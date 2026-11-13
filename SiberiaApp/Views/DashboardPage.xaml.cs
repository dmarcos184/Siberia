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
    /// Panel del administrador: ventas, ganancias y estado de la operacion.
    /// Todo sale de los pedidos y cortes reales; si algo no se puede calcular, se dice.
    /// </summary>
    public sealed partial class DashboardPage : Page
    {
        private const double AlturaMaximaBarra = 140;
        private bool _cargando;

        public DashboardPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await CargarAsync();
        }

        private async void BtnActualizar_Click(object sender, RoutedEventArgs e) => await CargarAsync();

        private async Task CargarAsync()
        {
            if (_cargando) return;
            _cargando = true;
            anilloCarga.IsActive = true;
            btnActualizar.IsEnabled = false;
            barraAviso.IsOpen = false;

            try
            {
                // Leer y sumar todos los pedidos se hace fuera del hilo de la pantalla
                PanelAdmin panel = await Task.Run(() => ReporteService.ObtenerPanelAdmin());
                Mostrar(panel);
                txtActualizado.Text = $"Actualizado a las {DateTime.Now:HH:mm} · datos reales de pedidos y cortes";
            }
            catch (Exception ex)
            {
                if (!Avisos.MostrarError(barraAviso, ex)) throw;
                txtActualizado.Text = "No se pudieron cargar los datos.";
            }
            finally
            {
                anilloCarga.IsActive = false;
                btnActualizar.IsEnabled = true;
                _cargando = false;
            }
        }

        private void Mostrar(PanelAdmin panel)
        {
            // ----- Ventas -----
            Llenar(txtVentasHoy, txtPedidosHoy, txtVarHoy, panel.Hoy);
            txtMetodosHoy.Text = panel.Hoy.Actual.TextoPorMetodo;
            Llenar(txtVentasSemana, txtPedidosSemana, txtVarSemana, panel.Semana);
            Llenar(txtVentasMes, txtPedidosMes, txtVarMes, panel.Mes);

            // ----- Ganancia: solo si hay costos capturados -----
            var mes = panel.Mes.Actual;
            if (mes.HayUtilidad)
            {
                txtUtilidadMes.Text = Formato.Dinero(mes.Utilidad);
            }
            else
            {
                txtUtilidadMes.Text = "No calculable";
            }
            txtNotaUtilidad.Text = mes.NotaUtilidad;

            // ----- Operacion -----
            txtEnCurso.Text = panel.PedidosEnCurso.ToString();
            txtCortesPendientes.Text = panel.CortesPendientes.ToString();
            txtUsuariosActivos.Text = panel.UsuariosActivos.ToString();

            // ----- Grafica de 7 dias -----
            decimal maximo = panel.UltimosSieteDias.Max(d => d.Ventas);
            graficaSemana.ItemsSource = panel.UltimosSieteDias.Select(d => new BarraGrafica
            {
                // la barra mas alta mide AlturaMaximaBarra; un dia sin ventas deja una linea de 2 px
                Altura = maximo <= 0 ? 2 : Math.Max(2, (double)(d.Ventas / maximo) * AlturaMaximaBarra),
                Valor = d.Ventas.ToString("C0"),
                Etiqueta = d.Fecha.ToString("ddd dd")
            }).ToList();

            // ----- Mas vendidos -----
            lstTopProductos.ItemsSource = panel.TopProductosMes;
            txtSinProductos.Visibility = panel.TopProductosMes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void Llenar(TextBlock ventas, TextBlock pedidos, TextBlock variacion, ComparacionPeriodos cmp)
        {
            ventas.Text = Formato.Dinero(cmp.Actual.Ventas);
            pedidos.Text = cmp.Actual.Cancelados > 0
                ? $"{cmp.Actual.Pedidos} pedidos · {cmp.Actual.Cancelados} cancelados"
                : $"{cmp.Actual.Pedidos} pedidos";
            variacion.Text = Formato.Variacion(cmp.VariacionVentas);
        }
    }
}
