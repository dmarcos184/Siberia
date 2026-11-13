using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SiberiaApp.Helpers;
using SiberiaApp.Models;
using SiberiaApp.Services;

namespace SiberiaApp.Views
{
    /// <summary>
    /// Reportes de ventas por dia, semana, mes o periodo personalizado.
    /// Usuario normal: solo sus ventas. Administrador: todo el negocio o un usuario, con ganancias.
    /// </summary>
    public sealed partial class ReportesPage : Page
    {
        private bool _cargando;

        public ReportesPage()
        {
            InitializeComponent();
            dtpReporte.Date = DateTimeOffset.Now; // hoy por defecto
            cmbPeriodo.SelectedIndex = 0;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            bool esAdmin = SesionService.EsAdmin;
            cmbUsuarioFiltro.Visibility = esAdmin ? Visibility.Visible : Visibility.Collapsed;
            tarjetaUtilidad.Visibility = esAdmin ? Visibility.Visible : Visibility.Collapsed;
            colUtilidad.Width = esAdmin ? new GridLength(1.3, GridUnitType.Star) : new GridLength(0);

            if (esAdmin)
                CargarUsuariosFiltro();
        }

        private void CargarUsuariosFiltro()
        {
            int? seleccionado = (cmbUsuarioFiltro.SelectedItem as OpcionUsuario)?.Id;

            List<Usuario> usuarios = null;
            if (!Avisos.Intentar(barraAviso, () => usuarios = UsuarioService.ObtenerTodos())) return;

            var opciones = new List<OpcionUsuario> { new OpcionUsuario { Id = null, Nombre = "Todos" } };
            opciones.AddRange(usuarios.Select(u => new OpcionUsuario { Id = u.Id, Nombre = u.NombreUsuario }));
            cmbUsuarioFiltro.ItemsSource = opciones;
            cmbUsuarioFiltro.SelectedItem = opciones.FirstOrDefault(o => o.Id == seleccionado) ?? opciones[0];
        }

        private TipoPeriodo PeriodoElegido => cmbPeriodo.SelectedIndex switch
        {
            1 => TipoPeriodo.Semana,
            2 => TipoPeriodo.Mes,
            3 => TipoPeriodo.Personalizado,
            _ => TipoPeriodo.Dia
        };

        private void CmbPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dtpHasta == null) return; // todavia se esta construyendo la pagina

            bool personalizado = PeriodoElegido == TipoPeriodo.Personalizado;
            dtpHasta.Visibility = personalizado ? Visibility.Visible : Visibility.Collapsed;
            dtpReporte.Header = personalizado ? "Desde" : "Fecha de referencia";
        }

        private async void BtnGenerar_Click(object sender, RoutedEventArgs e)
        {
            if (_cargando) return;

            DateTime referencia = dtpReporte.Date?.DateTime ?? DateTime.Today;
            DateTime? hasta = dtpHasta.Date?.DateTime;
            TipoPeriodo tipo = PeriodoElegido;
            int? usuarioId = (cmbUsuarioFiltro.SelectedItem as OpcionUsuario)?.Id;

            DateTime desdeFecha, hastaFecha;
            try
            {
                (desdeFecha, hastaFecha) = Periodos.Rango(tipo, referencia, referencia, hasta);
            }
            catch (ReglaNegocioException ex)
            {
                Avisos.Mostrar(barraAviso, ex.Message);
                return;
            }

            _cargando = true;
            btnGenerar.IsEnabled = false;
            anilloCarga.IsActive = true;
            anilloCarga.Visibility = Visibility.Visible;
            barraAviso.IsOpen = false;

            try
            {
                var (comparacion, pedidos) = await Task.Run(() =>
                    (ReporteService.Comparar(desdeFecha, hastaFecha, usuarioId),
                     PedidoService.Consultar(null, desdeFecha, hastaFecha, usuarioId)));

                lblTituloReporte.Text = tipo == TipoPeriodo.Dia
                    ? $"Ventas del {desdeFecha:dd/MM/yyyy}"
                    : $"Del {desdeFecha:dd/MM/yyyy} al {hastaFecha:dd/MM/yyyy}";

                Mostrar(comparacion, pedidos);
            }
            catch (Exception ex)
            {
                if (!Avisos.MostrarError(barraAviso, ex)) throw;
            }
            finally
            {
                _cargando = false;
                btnGenerar.IsEnabled = true;
                anilloCarga.IsActive = false;
                anilloCarga.Visibility = Visibility.Collapsed;
            }
        }

        private void Mostrar(ComparacionPeriodos cmp, List<Pedido> pedidos)
        {
            var r = cmp.Actual;

            dgvReporte.ItemsSource = pedidos.OrderBy(p => p.FechaHora).Select(FilaPedido.Desde).ToList();
            lstTopProductos.ItemsSource = r.TopProductos;

            lblTotalReporte.Text = Formato.Dinero(r.Ventas);
            lblVariacion.Text = Formato.Variacion(cmp.VariacionVentas);
            lblCantidadPedidosReporte.Text = r.Pedidos.ToString();
            lblCancelados.Text = r.Cancelados > 0
                ? $"{r.Cancelados} cancelados ({Formato.Dinero(r.MontoCancelado)}, no cuentan como venta)"
                : "Sin cancelaciones";
            lblPromedio.Text = Formato.Dinero(r.TicketPromedio);
            lblMetodos.Text = r.TextoPorMetodo;
            lblAjustes.Text = $"Descuentos {Formato.Dinero(r.Descuentos)}  ·  Devoluciones {Formato.Dinero(r.Devoluciones)}";

            lblUtilidad.Text = r.HayUtilidad ? Formato.Dinero(r.Utilidad) : "No calculable";
            lblNotaUtilidad.Text = r.NotaUtilidad;
        }
    }
}
