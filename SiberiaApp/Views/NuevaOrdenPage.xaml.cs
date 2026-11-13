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
using MenuItem = SiberiaApp.Models.MenuItem; // evita confusion con controles de WinUI

namespace SiberiaApp.Views
{
    /// <summary>
    /// Nueva orden: se toca un producto del catalogo para agregarlo, se ajusta la cantidad con + / −,
    /// se le ponen notas a cada producto y se confirma. Las reglas las valida PedidoService.
    /// </summary>
    public sealed partial class NuevaOrdenPage : Page
    {
        private const string TodasLasCategorias = "Todas";

        // Productos del pedido que se esta capturando
        private readonly List<DetallePedido> _detalles = new List<DetallePedido>();
        private List<MenuItem> _menu = new List<MenuItem>();

        // el ultimo pedido guardado, para poder guardar su ticket
        private Pedido _ultimoPedidoGuardado;
        private bool _guardando;

        public NuevaOrdenPage()
        {
            InitializeComponent();
            cmbTipoPedido.SelectedIndex = 0;

            CamposTexto.SinEspaciosRepetidos(txtCliente);
            CamposTexto.SinEspaciosRepetidos(txtDireccion);
            CamposTexto.SinEspaciosRepetidos(txtTelefono);

            RefrescarOrden();
        }

        // Se ejecuta cada vez que entras a esta pantalla desde el menu
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            CargarMenu(); // por si cambiaron precios en "Menu y precios"
        }

        // ---------------------------------------------------------------
        // Catalogo
        // ---------------------------------------------------------------
        private void CargarMenu()
        {
            List<MenuItem> menu = null;
            if (!Avisos.Intentar(barraAviso, () => menu = MenuService.ObtenerActivos())) return;

            _menu = menu.OrderBy(m => m.Categoria).ThenBy(m => m.Nombre).ToList();

            string categoriaActual = cmbCategoria.SelectedItem as string;
            var categorias = new List<string> { TodasLasCategorias };
            categorias.AddRange(_menu.Select(m => m.Categoria).Distinct().OrderBy(c => c));
            cmbCategoria.ItemsSource = categorias;
            cmbCategoria.SelectedItem = categorias.Contains(categoriaActual) ? categoriaActual : TodasLasCategorias;

            FiltrarProductos();

            string cambios = SincronizarConMenu();
            if (cambios != null)
            {
                RefrescarOrden();
                Avisos.Mostrar(barraAviso, cambios, InfoBarSeverity.Informational);
            }
        }

        private void FiltrarProductos()
        {
            if (gridProductos == null) return; // todavia se esta construyendo la pagina

            IEnumerable<MenuItem> q = _menu;

            string categoria = cmbCategoria.SelectedItem as string;
            if (!string.IsNullOrEmpty(categoria) && categoria != TodasLasCategorias)
                q = q.Where(m => m.Categoria == categoria);

            string texto = txtBuscarProducto.Text?.Trim();
            if (!string.IsNullOrEmpty(texto))
                q = q.Where(m => m.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

            gridProductos.ItemsSource = q.ToList();
        }

        private void TxtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e) => FiltrarProductos();

        private void CmbCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e) => FiltrarProductos();

        /// <summary>
        /// Pone al dia los renglones con el menu actual (precio nuevo, producto retirado).
        /// Devuelve el mensaje de lo que cambio, o null si no cambio nada.
        /// </summary>
        private string SincronizarConMenu()
        {
            var mensajes = new List<string>();

            foreach (var d in _detalles.ToList())
            {
                var item = _menu.FirstOrDefault(m => m.Id == d.MenuItemId);
                if (item == null)
                {
                    _detalles.Remove(d);
                    mensajes.Add($"\"{d.NombreProducto}\" ya no está disponible y se quitó del pedido.");
                }
                else if (item.Precio != d.PrecioUnitario)
                {
                    mensajes.Add($"El precio de \"{d.NombreProducto}\" cambió de {Formato.Dinero(d.PrecioUnitario)} a {Formato.Dinero(item.Precio)}.");
                    d.PrecioUnitario = item.Precio;
                }
            }

            return mensajes.Count == 0 ? null : string.Join(" ", mensajes);
        }

        // ---------------------------------------------------------------
        // Tipo de pedido
        // ---------------------------------------------------------------
        private void CmbTipoPedido_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelDomicilio == null) return; // todavia se esta construyendo la pagina

            bool esDomicilio = cmbTipoPedido.SelectedItem as string == TiposPedido.Domicilio;
            panelDomicilio.Visibility = esDomicilio ? Visibility.Visible : Visibility.Collapsed;
            if (!esDomicilio)
            {
                txtDireccion.Text = string.Empty;
                txtTelefono.Text = string.Empty;
            }
        }

        // ---------------------------------------------------------------
        // Carrito
        // ---------------------------------------------------------------
        private void GridProductos_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not MenuItem producto) return;

            var existente = _detalles.FirstOrDefault(d => d.MenuItemId == producto.Id);
            if (existente != null)
            {
                if (existente.Cantidad >= PedidoService.CantidadMaximaPorProducto)
                {
                    Avisos.Mostrar(barraAviso,
                        $"Solo se pueden agregar hasta {PedidoService.CantidadMaximaPorProducto} unidades de \"{producto.Nombre}\".");
                    return;
                }
                existente.Cantidad++;
            }
            else
            {
                _detalles.Add(new DetallePedido
                {
                    MenuItemId = producto.Id,
                    NombreProducto = producto.Nombre,
                    PrecioUnitario = producto.Precio,
                    Cantidad = 1
                });
            }

            barraAviso.IsOpen = false;
            RefrescarOrden();
        }

        private static DetallePedido DetalleDe(object sender) => (sender as FrameworkElement)?.Tag as DetallePedido;

        private void BtnMas_Click(object sender, RoutedEventArgs e)
        {
            var d = DetalleDe(sender);
            if (d == null) return;

            if (d.Cantidad >= PedidoService.CantidadMaximaPorProducto)
            {
                Avisos.Mostrar(barraAviso,
                    $"Solo se pueden agregar hasta {PedidoService.CantidadMaximaPorProducto} unidades de \"{d.NombreProducto}\".");
                return;
            }
            d.Cantidad++;
            barraAviso.IsOpen = false;
            RefrescarOrden();
        }

        private void BtnMenos_Click(object sender, RoutedEventArgs e)
        {
            var d = DetalleDe(sender);
            if (d == null) return;

            if (d.Cantidad <= 1)
            {
                Avisos.Mostrar(barraAviso, "La cantidad mínima es 1. Usa \"Quitar\" para sacar el producto del pedido.",
                    InfoBarSeverity.Informational);
                return;
            }

            d.Cantidad--;

            // una nota no puede aplicar a mas unidades de las que quedan
            bool ajustadas = false;
            foreach (var nota in d.Notas.Where(n => n.Cantidad > d.Cantidad))
            {
                nota.Cantidad = d.Cantidad;
                ajustadas = true;
            }

            if (ajustadas)
                Avisos.Mostrar(barraAviso, $"Se ajustaron las notas de \"{d.NombreProducto}\" a la nueva cantidad.",
                    InfoBarSeverity.Informational);
            else
                barraAviso.IsOpen = false;

            RefrescarOrden();
        }

        private void BtnQuitar_Click(object sender, RoutedEventArgs e)
        {
            var d = DetalleDe(sender);
            if (d == null) return;

            _detalles.Remove(d);
            barraAviso.IsOpen = false;
            RefrescarOrden();
        }

        private async void BtnNotas_Click(object sender, RoutedEventArgs e)
        {
            var d = DetalleDe(sender);
            if (d == null) return;

            if (await NotasDialog.MostrarAsync(XamlRoot, d))
                RefrescarOrden();
        }

        // Vuelve a dibujar la lista y recalcula el total
        private void RefrescarOrden()
        {
            lstOrden.ItemsSource = null;
            lstOrden.ItemsSource = _detalles.ToList();

            ActualizarTotales();
            int unidades = _detalles.Sum(d => d.Cantidad);

            lblArticulos.Text = unidades == 1 ? "1 producto" : $"{unidades} productos";
            lblVacio.Visibility = _detalles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------------------------------------------------------
        // Descuento y totales
        // ---------------------------------------------------------------
        private decimal LeerDescuento() =>
            double.IsNaN(txtDescuento.Value) ? 0m : Math.Round((decimal)txtDescuento.Value, 2);

        // Solo muestra los numeros; las reglas del descuento las valida PedidoService al guardar
        private void ActualizarTotales()
        {
            if (lblTotalOrden == null) return; // todavia se esta construyendo la pagina

            decimal subtotal = _detalles.Sum(d => d.Subtotal);
            decimal descuento = LeerDescuento();

            lblSubtotalOrden.Text = Formato.Dinero(subtotal);
            filaDescuento.Visibility = descuento > 0 ? Visibility.Visible : Visibility.Collapsed;
            lblDescuentoOrden.Text = "-" + Formato.Dinero(descuento);
            lblTotalOrden.Text = Formato.Dinero(Math.Max(0m, subtotal - descuento));
        }

        private void TxtDescuento_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            ActualizarTotales();
        }

        // ---------------------------------------------------------------
        // Guardar
        // ---------------------------------------------------------------
        private void PonerGuardando(bool guardando)
        {
            _guardando = guardando;
            btnGuardarPedido.IsEnabled = !guardando;
            btnLimpiarOrden.IsEnabled = !guardando;
            anilloGuardando.IsActive = guardando;
            anilloGuardando.Visibility = guardando ? Visibility.Visible : Visibility.Collapsed;
            iconoGuardar.Visibility = guardando ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void BtnGuardarPedido_Click(object sender, RoutedEventArgs e)
        {
            if (_guardando) return; // evita guardar dos veces por doble clic

            decimal descuento = LeerDescuento();
            decimal total = _detalles.Sum(d => d.Subtotal) - descuento;
            string metodoPago = cmbMetodoPago.SelectedItem as string ?? string.Empty;

            // Confirmacion con resumen (solo si hay algo que confirmar; si no, el servicio explica que falta)
            if (_detalles.Count > 0)
            {
                string cliente = string.IsNullOrWhiteSpace(txtCliente.Text) ? "(sin nombre)" : txtCliente.Text.Trim();
                string resumen = $"Cliente: {cliente}\n" +
                                 $"{_detalles.Sum(d => d.Cantidad)} producto(s)\n" +
                                 (descuento > 0 ? $"Descuento: {Formato.Dinero(descuento)}\n" : string.Empty) +
                                 $"Total: {Formato.Dinero(total)}\n" +
                                 $"Pago: {metodoPago}\n\n¿Guardar el pedido?";
                if (!await Avisos.ConfirmarAsync(XamlRoot, "Confirmar pedido", resumen))
                    return;
            }

            PonerGuardando(true);
            try
            {
                var pedido = new Pedido
                {
                    Cliente = txtCliente.Text,
                    TipoPedido = cmbTipoPedido.SelectedItem as string ?? TiposPedido.Mostrador,
                    Direccion = txtDireccion.Text,
                    Telefono = txtTelefono.Text,
                    MetodoPago = metodoPago,
                    Descuento = descuento,
                    Detalles = _detalles.ToList()
                };

                // El servicio vuelve a validar todo y recalcula el total; aqui solo le decimos que total vio la persona
                Pedido guardado = await Task.Run(() => PedidoService.Guardar(pedido, total));

                _ultimoPedidoGuardado = guardado;
                txtTicketPreview.Text = PedidoService.GenerarTicket(guardado);
                expTicket.IsExpanded = true;

                Avisos.Exito(barraAviso, $"Pedido guardado correctamente. Folio: {guardado.Folio}");
                LimpiarOrdenActual(mantenerTicket: true);
            }
            catch (PrecioCambiadoException ex)
            {
                // el menu cambio mientras se capturaba: se actualizan los precios y se pide revisar
                SincronizarConMenu();
                CargarMenu();
                RefrescarOrden();
                Avisos.Mostrar(barraAviso, ex.Message);
            }
            catch (Exception ex)
            {
                if (!Avisos.MostrarError(barraAviso, ex)) throw;
            }
            finally
            {
                PonerGuardando(false);
            }
        }

        private async void BtnLimpiarOrden_Click(object sender, RoutedEventArgs e)
        {
            bool hayAlgo = _detalles.Count > 0 || !string.IsNullOrWhiteSpace(txtCliente.Text);
            if (hayAlgo &&
                !await Avisos.ConfirmarAsync(XamlRoot, "Limpiar pedido", "¿Borrar el pedido que estás capturando?"))
                return;

            LimpiarOrdenActual();
            barraAviso.IsOpen = false;
        }

        private void LimpiarOrdenActual(bool mantenerTicket = false)
        {
            txtCliente.Text = string.Empty;
            cmbTipoPedido.SelectedIndex = 0;
            txtDireccion.Text = string.Empty;
            txtTelefono.Text = string.Empty;
            cmbMetodoPago.SelectedIndex = 0;
            txtDescuento.Value = 0;
            _detalles.Clear();
            RefrescarOrden();
            if (!mantenerTicket)
            {
                txtTicketPreview.Text = string.Empty;
                _ultimoPedidoGuardado = null;
            }
        }

        // ---------------------------------------------------------------
        // Ticket .txt
        // ---------------------------------------------------------------
        private void BtnGuardarTicketTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_ultimoPedidoGuardado == null)
            {
                Avisos.Mostrar(barraAviso, "Primero guarda un pedido para generar su ticket.");
                return;
            }

            try
            {
                string ruta = TicketService.GuardarTicketTxt(_ultimoPedidoGuardado);
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
