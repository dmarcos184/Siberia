using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SiberiaApp.Helpers;
using SiberiaApp.Models;
using SiberiaApp.Services;
using Windows.System;

namespace SiberiaApp.Views
{
    /// <summary>
    /// Consultar pedidos guardados. Un usuario normal ve solo los suyos;
    /// el administrador ve todos y puede filtrar por usuario.
    /// </summary>
    public sealed partial class ConsultarPedidosPage : Page
    {
        // pedidos que se estan mostrando (para sacar el ticket sin volver a leer el archivo)
        private List<Pedido> _pedidosMostrados = new List<Pedido>();

        public ConsultarPedidosPage()
        {
            InitializeComponent();
            CamposTexto.SinEspaciosRepetidos(txtBuscarPedido);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // El filtro por usuario existe solo para administradores (y el servicio lo vuelve a exigir)
            bool esAdmin = SesionService.EsAdmin;
            cmbUsuarioFiltro.Visibility = esAdmin ? Visibility.Visible : Visibility.Collapsed;
            if (esAdmin)
                CargarUsuariosFiltro();

            RefrescarPedidos(); // siempre muestra los pedidos mas recientes
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

        private void RefrescarPedidos()
        {
            DateTime? desde = dtpDesde.Date?.DateTime;
            DateTime? hasta = dtpHasta.Date?.DateTime;
            if (desde.HasValue && hasta.HasValue && hasta.Value.Date < desde.Value.Date)
            {
                Avisos.Mostrar(barraAviso, "La fecha final no puede ser anterior a la inicial.");
                return;
            }

            int? usuarioId = (cmbUsuarioFiltro.SelectedItem as OpcionUsuario)?.Id;

            List<Pedido> pedidos = null;
            if (!Avisos.Intentar(barraAviso, () =>
                    pedidos = PedidoService.Consultar(txtBuscarPedido.Text, desde, hasta, usuarioId)))
                return;

            barraAviso.IsOpen = false;
            _pedidosMostrados = pedidos;
            dgvPedidos.ItemsSource = pedidos.Select(FilaPedido.Desde).ToList();
            txtDetallePedido.Text = string.Empty;
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e) => RefrescarPedidos();

        private void BtnMostrarTodos_Click(object sender, RoutedEventArgs e)
        {
            txtBuscarPedido.Text = string.Empty;
            dtpDesde.Date = null;
            dtpHasta.Date = null;
            if (cmbUsuarioFiltro.ItemsSource is List<OpcionUsuario> opciones && opciones.Count > 0)
                cmbUsuarioFiltro.SelectedItem = opciones[0];
            RefrescarPedidos();
        }

        // Enter en la caja de busqueda = Buscar
        private void TxtBuscarPedido_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                RefrescarPedidos();
        }

        private Pedido PedidoSeleccionado() =>
            dgvPedidos.SelectedItem is FilaPedido fila
                ? _pedidosMostrados.FirstOrDefault(p => p.Id == fila.Id)
                : null;

        private void DgvPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pedido = PedidoSeleccionado();
            if (pedido != null)
                txtDetallePedido.Text = PedidoService.GenerarTicket(pedido);
        }

        // ========================================================
        // Guardar el ticket del pedido seleccionado como .txt
        // ========================================================
        private void BtnGuardarTicketTxt_Click(object sender, RoutedEventArgs e)
        {
            var pedido = PedidoSeleccionado();
            if (pedido == null)
            {
                Avisos.Mostrar(barraAviso, "Selecciona un pedido de la lista.");
                return;
            }

            try
            {
                string ruta = TicketService.GuardarTicketTxt(pedido);
                Avisos.Exito(barraAviso, $"Ticket guardado en: {ruta}");
            }
            catch (Exception ex)
            {
                Avisos.Mostrar(barraAviso, $"No se pudo guardar el ticket: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void BtnAbrirCarpetaTickets_Click(object sender, RoutedEventArgs e)
        {
            TicketService.AbrirCarpeta();
        }
    }
}
