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
    /// Cortes de caja. Usuario normal: sus dias por cerrar y su historial.
    /// Administrador: todos los usuarios, filtros por usuario/fecha y reapertura de cortes.
    /// </summary>
    public sealed partial class CortesPage : Page
    {
        private DiaPorCerrar _diaSeleccionado;
        private Corte _corteSeleccionado;
        private bool _cambiandoSeleccion; // evita que las dos listas se disparen entre si
        private bool _cerrando;

        public CortesPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            bool esAdmin = SesionService.EsAdmin;
            cmbUsuarioFiltro.Visibility = esAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnReabrirCorte.Visibility = esAdmin ? Visibility.Visible : Visibility.Collapsed;
            lblAyudaDias.Text = esAdmin
                ? "Días trabajados de todos los usuarios que aún no tienen corte cerrado."
                : "Solo aparecen los días en que tuviste pedidos y aún no cierras tu corte.";

            if (esAdmin)
                CargarUsuariosFiltro();

            Refrescar();
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

        // ---------------------------------------------------------------
        // Cargar listas
        // ---------------------------------------------------------------
        private void Refrescar()
        {
            List<DiaPorCerrar> dias = null;
            if (!Avisos.Intentar(barraAviso, () => dias = CorteService.ObtenerDiasPendientes())) return;

            DateTime? desde = dtpDesde.Date?.DateTime;
            DateTime? hasta = dtpHasta.Date?.DateTime;
            if (desde.HasValue && hasta.HasValue && hasta.Value.Date < desde.Value.Date)
            {
                Avisos.Mostrar(barraAviso, "La fecha final no puede ser anterior a la inicial.");
                return;
            }
            int? usuarioId = (cmbUsuarioFiltro.SelectedItem as OpcionUsuario)?.Id;

            List<Corte> cortes = null;
            if (!Avisos.Intentar(barraAviso, () => cortes = CorteService.ObtenerCortes(desde, hasta, usuarioId))) return;

            _cambiandoSeleccion = true;
            try
            {
                lstDias.ItemsSource = dias;
                lblSinDias.Visibility = dias.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                lstHistorial.ItemsSource = cortes;
            }
            finally { _cambiandoSeleccion = false; }

            LimpiarDetalle();
        }

        private void LimpiarDetalle()
        {
            _diaSeleccionado = null;
            _corteSeleccionado = null;
            txtDetalleCorte.Text = string.Empty;
            txtObservaciones.Text = string.Empty;
            txtObservaciones.IsEnabled = false;
            btnCerrarCorte.IsEnabled = false;
            btnReabrirCorte.IsEnabled = false;
            lblMotivoCorte.Text = string.Empty;
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e) => Refrescar();

        private void BtnQuitarFiltros_Click(object sender, RoutedEventArgs e)
        {
            dtpDesde.Date = null;
            dtpHasta.Date = null;
            if (cmbUsuarioFiltro.ItemsSource is List<OpcionUsuario> opciones && opciones.Count > 0)
                cmbUsuarioFiltro.SelectedItem = opciones[0];
            Refrescar();
        }

        // ---------------------------------------------------------------
        // Seleccion
        // ---------------------------------------------------------------
        private void LstDias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cambiandoSeleccion || lstDias.SelectedItem is not DiaPorCerrar dia) return;

            _cambiandoSeleccion = true;
            lstHistorial.SelectedItem = null;
            _cambiandoSeleccion = false;

            _diaSeleccionado = dia;
            _corteSeleccionado = null;
            btnReabrirCorte.IsEnabled = false;

            Corte vista = null;
            List<Pedido> pedidos = null;
            if (!Avisos.Intentar(barraAviso, () =>
                {
                    vista = CorteService.Previsualizar(dia.Fecha, dia.UsuarioId);
                    pedidos = PedidoService.Consultar(null, dia.Fecha, dia.Fecha, dia.UsuarioId);
                }))
            {
                LimpiarDetalle();
                return;
            }

            txtDetalleCorte.Text = CorteService.GenerarTexto(vista, pedidos);

            // Solo se puede cerrar si todos los pedidos del dia estan finalizados
            txtObservaciones.IsEnabled = dia.ListoParaCerrar;
            btnCerrarCorte.IsEnabled = dia.ListoParaCerrar;
            lblMotivoCorte.Text = dia.ListoParaCerrar
                ? "Al cerrar el corte, las ventas de ese día ya no se podrán modificar."
                : $"Hay {dia.SinFinalizar} pedido(s) sin finalizar. Márcalos como Entregado o Cancelado para poder cerrar.";
        }

        private void LstHistorial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cambiandoSeleccion || lstHistorial.SelectedItem is not Corte corte) return;

            _cambiandoSeleccion = true;
            lstDias.SelectedItem = null;
            _cambiandoSeleccion = false;

            _diaSeleccionado = null;
            _corteSeleccionado = corte;
            btnCerrarCorte.IsEnabled = false;
            txtObservaciones.IsEnabled = false;
            txtObservaciones.Text = corte.Observaciones;

            List<Pedido> pedidos = null;
            if (!Avisos.Intentar(barraAviso, () => pedidos = CorteService.ObtenerPedidosDelCorte(corte.Id)))
            {
                LimpiarDetalle();
                return;
            }

            txtDetalleCorte.Text = CorteService.GenerarTexto(corte, pedidos);
            btnReabrirCorte.IsEnabled = SesionService.EsAdmin && corte.EstaCerrado;
            lblMotivoCorte.Text = corte.EstaCerrado
                ? "Corte cerrado: sus ventas no se pueden modificar."
                : "Este corte fue reabierto. Hay que volver a cerrarlo desde \"Días por cerrar\".";
        }

        // ---------------------------------------------------------------
        // Cerrar
        // ---------------------------------------------------------------
        private async void BtnCerrarCorte_Click(object sender, RoutedEventArgs e)
        {
            if (_cerrando || _diaSeleccionado == null) return;
            var dia = _diaSeleccionado;

            bool confirmar = await Avisos.ConfirmarAsync(XamlRoot, "Cerrar corte",
                $"¿Cerrar el corte de {dia.NombreUsuario} del {dia.Fecha:dd/MM/yyyy}?\n\n" +
                "Una vez cerrado ya no se podrán modificar las ventas de ese día " +
                "(solo un administrador puede reabrirlo).");
            if (!confirmar) return;

            _cerrando = true;
            btnCerrarCorte.IsEnabled = false;
            anilloCierre.IsActive = true;
            anilloCierre.Visibility = Visibility.Visible;

            try
            {
                string observaciones = txtObservaciones.Text;
                Corte cerrado = await Task.Run(() => CorteService.CerrarCorte(dia.Fecha, observaciones, dia.UsuarioId));

                Refrescar();
                Avisos.Exito(barraAviso, $"Corte del {cerrado.Fecha:dd/MM/yyyy} cerrado. Total neto: {Formato.Dinero(cerrado.TotalNeto)}.");
            }
            catch (Exception ex)
            {
                if (!Avisos.MostrarError(barraAviso, ex)) throw;
                // sigue seleccionado el mismo dia: se vuelve a permitir intentar
                btnCerrarCorte.IsEnabled = _diaSeleccionado != null && _diaSeleccionado.ListoParaCerrar;
            }
            finally
            {
                _cerrando = false;
                anilloCierre.IsActive = false;
                anilloCierre.Visibility = Visibility.Collapsed;
            }
        }

        // ---------------------------------------------------------------
        // Reabrir (solo administrador)
        // ---------------------------------------------------------------
        private async void BtnReabrirCorte_Click(object sender, RoutedEventArgs e)
        {
            if (_corteSeleccionado == null) return;
            var corte = _corteSeleccionado;

            string motivo = await Avisos.PedirTextoAsync(XamlRoot, "Reabrir corte",
                $"Corte de {corte.NombreUsuario} del {corte.Fecha:dd/MM/yyyy}. " +
                "Escribe el motivo; quedará registrado junto con tu usuario.",
                "Ej. se capturó mal un pedido", CorteService.LongitudMaximaObservaciones);
            if (motivo == null) return;

            if (!Avisos.Intentar(barraAviso, () => CorteService.ReabrirCorte(corte.Id, motivo))) return;

            Refrescar();
            Avisos.Exito(barraAviso, "Corte reabierto. El usuario (o un administrador) debe volver a cerrarlo.");
        }
    }
}
