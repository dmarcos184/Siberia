using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SiberiaApp.Helpers;
using SiberiaApp.Models;
using SiberiaApp.Services;

namespace SiberiaApp.Views
{
    /// <summary>Actualizar el estado de un pedido (con las reglas de permisos del servicio).</summary>
    public sealed partial class ActualizarPedidoPage : Page
    {
        private int _pedidoIdSeleccionado = 0;
        private List<Pedido> _pedidos = new List<Pedido>();

        public ActualizarPedidoPage()
        {
            InitializeComponent();
            ActualizarControles(null);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            panelDevolucion.Visibility = SesionService.EsAdmin ? Visibility.Visible : Visibility.Collapsed;
            RefrescarPedidos();
        }

        private void RefrescarPedidos()
        {
            List<Pedido> pedidos = null;
            if (!Avisos.Intentar(barraAviso, () => pedidos = PedidoService.ObtenerTodos())) return;

            _pedidos = pedidos;
            dgvActualizar.ItemsSource = pedidos.Select(FilaPedido.Desde).ToList();

            _pedidoIdSeleccionado = 0;
            txtDetalle.Text = string.Empty;
            ActualizarControles(null);
        }

        // Muestra el folio, deshabilita lo que no se puede hacer y dice por que
        private void ActualizarControles(Pedido pedido)
        {
            if (pedido == null)
            {
                lblFolioSeleccionado.Text = "Pedido seleccionado: (ninguno)";
                lblMotivoBloqueo.Visibility = Visibility.Collapsed;
                cmbNuevoEstado.IsEnabled = false;
                btnActualizarEstado.IsEnabled = false;
                btnRegistrarDevolucion.IsEnabled = false;
                lblDevuelto.Text = string.Empty;
                return;
            }

            // Devolucion: solo pedidos entregados con algo por devolver (el servicio valida el resto)
            decimal disponible = pedido.Total - pedido.MontoDevuelto;
            btnRegistrarDevolucion.IsEnabled = pedido.Estado == EstadosPedido.Entregado && disponible > 0;
            lblDevuelto.Text = $"Cobrado: {Formato.Dinero(pedido.Total)}  ·  Ya devuelto: {Formato.Dinero(pedido.MontoDevuelto)}  ·  " +
                               $"Por devolver como máximo: {Formato.Dinero(disponible)}";

            lblFolioSeleccionado.Text = $"Pedido seleccionado: {pedido.Folio}";

            string motivo = null;
            Avisos.Intentar(barraAviso, () => motivo = PedidoService.MotivoNoEditable(pedido));

            bool editable = motivo == null;
            cmbNuevoEstado.IsEnabled = editable;
            btnActualizarEstado.IsEnabled = editable;
            lblMotivoBloqueo.Text = motivo ?? string.Empty;
            lblMotivoBloqueo.Visibility = editable ? Visibility.Collapsed : Visibility.Visible;
        }

        private void DgvActualizar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvActualizar.SelectedItem is not FilaPedido fila) return;

            var pedido = _pedidos.FirstOrDefault(p => p.Id == fila.Id);
            if (pedido == null) return;

            _pedidoIdSeleccionado = pedido.Id;
            txtDetalle.Text = PedidoService.GenerarTicket(pedido);
            ActualizarControles(pedido);

            int indice = Array.IndexOf(EstadosPedido.Todos, pedido.Estado);
            if (indice >= 0)
                cmbNuevoEstado.SelectedIndex = indice;
        }

        private async void BtnActualizarEstado_Click(object sender, RoutedEventArgs e)
        {
            if (_pedidoIdSeleccionado == 0)
            {
                Avisos.Mostrar(barraAviso, "Selecciona un pedido de la lista.");
                return;
            }
            if (cmbNuevoEstado.SelectedItem is not string nuevoEstado)
            {
                Avisos.Mostrar(barraAviso, "Selecciona el nuevo estado.");
                return;
            }

            var pedido = _pedidos.FirstOrDefault(p => p.Id == _pedidoIdSeleccionado);
            if (pedido == null) return;

            // Validacion: Si el estado seleccionado es el mismo que ya tiene el pedido
            if (pedido.Estado == nuevoEstado)
            {
                Avisos.Mostrar(barraAviso, $"El pedido ya se encuentra en estado \"{nuevoEstado}\".");
                return;
            }

            // Cancelar o entregar cierra el pedido: se confirma antes
            if (EstadosPedido.EsFinal(nuevoEstado))
            {
                bool confirmar = await Avisos.ConfirmarAsync(XamlRoot, "Confirmar cambio",
                    $"¿Marcar el pedido {pedido.Folio} como \"{nuevoEstado}\"? " +
                    "Después de esto solo un administrador podrá modificarlo.");
                if (!confirmar) return;
            }

            if (!Avisos.Intentar(barraAviso, () => PedidoService.CambiarEstado(pedido.Id, nuevoEstado)))
                return;

            RefrescarPedidos();
            Avisos.Exito(barraAviso, $"Estado del pedido {pedido.Folio} actualizado a \"{nuevoEstado}\".");
        }

        private async void BtnRegistrarDevolucion_Click(object sender, RoutedEventArgs e)
        {
            var pedido = _pedidos.FirstOrDefault(p => p.Id == _pedidoIdSeleccionado);
            if (pedido == null)
            {
                Avisos.Mostrar(barraAviso, "Selecciona un pedido de la lista.");
                return;
            }

            decimal monto = double.IsNaN(txtMontoDevolucion.Value) ? 0m : Math.Round((decimal)txtMontoDevolucion.Value, 2);
            string motivo = txtMotivoDevolucion.Text;

            // Si falta el monto o el motivo, el servicio explica que falta (sin pedir confirmacion antes)
            if (monto > 0 && motivo.Trim().Length >= 5)
            {
                bool confirmar = await Avisos.ConfirmarAsync(XamlRoot, "Confirmar devolución",
                    $"¿Registrar una devolución de {Formato.Dinero(monto)} del pedido {pedido.Folio}? " +
                    "Este dinero se restará de las ventas del día.");
                if (!confirmar) return;
            }

            if (!Avisos.Intentar(barraAviso, () => PedidoService.RegistrarDevolucion(pedido.Id, monto, motivo)))
                return;

            txtMontoDevolucion.Value = 0;
            txtMotivoDevolucion.Text = string.Empty;
            RefrescarPedidos();
            Avisos.Exito(barraAviso, $"Devolución de {Formato.Dinero(monto)} registrada en el pedido {pedido.Folio}.");
        }
    }
}
