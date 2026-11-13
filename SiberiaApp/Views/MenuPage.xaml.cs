using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SiberiaApp.Helpers;
using SiberiaApp.Services;
using MenuItem = SiberiaApp.Models.MenuItem; // evita confusion con controles de WinUI

namespace SiberiaApp.Views
{
    /// <summary>Pestaña 2: Menu y precios (CRUD del catalogo).</summary>
    public sealed partial class MenuPage : Page
    {
        private int _menuIdSeleccionado = 0;
        private List<string> _categorias = new List<string>();

        // Permite asignar texto por codigo (al seleccionar/limpiar) sin que lo bloquee la validacion
        private bool _cargando = false;

        // Evita enganchar los eventos mas de una vez (Loaded puede dispararse varias veces)
        private bool _enganchado = false;

        public MenuPage()
        {
            InitializeComponent();
            Loaded += MenuPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefrescarMenu();
        }

        // =====================================================
        //  REGLAS DE VALIDACION
        // =====================================================

        /// <summary>
        /// Texto valido: solo letras y espacios, no empieza con espacio
        /// (sin texto no se pueden poner espacios) y nunca 3 o mas espacios seguidos.
        /// </summary>
        private static bool TextoPermitido(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return true;
            if (texto[0] == ' ') return false;                 // espacios sin texto
            if (texto.Contains("   ")) return false;          // mas de dos espacios
            return texto.All(c => char.IsLetter(c) || c == ' ');
        }

        /// <summary>Precio valido: solo digitos 0-9 y maximo un punto con 2 decimales.</summary>
        private static bool PrecioPermitido(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return true;

            int puntos = 0;
            int decimales = 0;
            foreach (char c in texto)
            {
                if (c == '.')
                {
                    puntos++;
                    if (puntos > 1) return false;
                }
                else if (c >= '0' && c <= '9')
                {
                    if (puntos == 1) decimales++;
                    if (decimales > 2) return false;
                }
                else
                {
                    return false;                              // letras, espacios, simbolos
                }
            }
            return true;
        }

        // =====================================================
        //  ENGANCHE DE LA VALIDACION (por codigo)
        // =====================================================

        private void MenuPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_enganchado) return;
            _enganchado = true;

            Enganchar(txtMenuNombre, TextoPermitido);
            Enganchar(txtMenuPrecio, PrecioPermitido);
            Enganchar(txtMenuCosto, PrecioPermitido);
            Enganchar(cmbMenuCategoria, TextoPermitido);
        }

        /// <summary>
        /// Conecta la regla al TextBox del control. Funciona si el control es un TextBox,
        /// un ComboBox editable o un NumberBox (busca su TextBox interno).
        /// </summary>
        private void Enganchar(Control control, Func<string, bool> regla)
        {
            control.ApplyTemplate();

            TextBox caja = control as TextBox ?? BuscarTextBox(control);
            if (caja == null) return;

            caja.BeforeTextChanging += (s, args) =>
            {
                if (_cargando) return;
                if (!regla(args.NewText))
                    args.Cancel = true;
            };
        }

        private static TextBox BuscarTextBox(DependencyObject padre)
        {
            int total = VisualTreeHelper.GetChildrenCount(padre);
            for (int i = 0; i < total; i++)
            {
                var hijo = VisualTreeHelper.GetChild(padre, i);
                if (hijo is TextBox caja) return caja;

                var resultado = BuscarTextBox(hijo);
                if (resultado != null) return resultado;
            }
            return null;
        }

        // =====================================================
        //  CRUD
        // =====================================================

        private void RefrescarMenu()
        {
            List<MenuItem> menu = null;
            if (!Avisos.Intentar(barraAviso, () => menu = MenuService.ObtenerTodos())) return;

            dgvMenu.ItemsSource = null;
            dgvMenu.ItemsSource = menu;

            // Las categorias del combo salen de los productos que ya existen
            _categorias = menu
                .Select(m => m.Categoria)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            cmbMenuCategoria.ItemsSource = _categorias;
        }

        private void DgvMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvMenu.SelectedItem is MenuItem item)
            {
                _cargando = true;
                try
                {
                    _menuIdSeleccionado = item.Id;
                    txtMenuNombre.Text = item.Nombre;
                    EscribirCategoria(item.Categoria);
                    txtMenuPrecio.Text = item.Precio.ToString("0.##", CultureInfo.InvariantCulture);
                    txtMenuCosto.Text = item.Costo.HasValue ? item.Costo.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
                    chkMenuActivo.IsOn = item.Activo;
                    lblModo.Text = $"Editando: {item.Nombre}";
                }
                finally { _cargando = false; }
            }
        }

        // ---- Leer / escribir la categoria del ComboBox editable ----
        private string LeerCategoria()
        {
            string texto = cmbMenuCategoria.Text;
            if (string.IsNullOrWhiteSpace(texto))
                texto = cmbMenuCategoria.SelectedItem as string ?? string.Empty;
            return texto.Trim();
        }

        private void EscribirCategoria(string categoria)
        {
            bool estabaCargando = _cargando;
            _cargando = true;
            try
            {
                cmbMenuCategoria.SelectedItem = null;
                if (_categorias.Contains(categoria))
                    cmbMenuCategoria.SelectedItem = categoria;
                cmbMenuCategoria.Text = categoria ?? string.Empty;
            }
            finally { _cargando = estabaCargando; }
        }

        private bool ValidarCamposMenu(out decimal precio, out decimal? costo)
        {
            precio = 0;
            costo = null;

            // --- Nombre ---
            string nombre = txtMenuNombre.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                Avisos.Mostrar(barraAviso, "Escribe el nombre del producto.");
                return false;
            }
            if (!TextoPermitido(nombre))
            {
                Avisos.Mostrar(barraAviso, "El nombre solo puede tener letras (sin números ni símbolos).");
                return false;
            }

            // --- Categoria ---
            string categoria = LeerCategoria();
            if (string.IsNullOrWhiteSpace(categoria))
            {
                Avisos.Mostrar(barraAviso, "Escribe o selecciona una categoría.");
                return false;
            }
            if (!TextoPermitido(categoria))
            {
                Avisos.Mostrar(barraAviso, "La categoría solo puede tener letras (sin números ni símbolos).");
                return false;
            }

            // --- Precio ---
            string textoPrecio = txtMenuPrecio.Text.Trim();
            if (string.IsNullOrEmpty(textoPrecio) ||
                !PrecioPermitido(textoPrecio) ||
                !decimal.TryParse(textoPrecio, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor))
            {
                Avisos.Mostrar(barraAviso, "Captura un precio válido (solo números).");
                return false;
            }

            precio = Math.Round(valor, 2);

            // --- Costo (opcional: vacio = sin capturar) ---
            string textoCosto = txtMenuCosto.Text.Trim();
            if (textoCosto.Length > 0)
            {
                if (!PrecioPermitido(textoCosto) ||
                    !decimal.TryParse(textoCosto, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valorCosto))
                {
                    Avisos.Mostrar(barraAviso, "Captura un costo válido (solo números) o déjalo vacío.");
                    return false;
                }
                costo = Math.Round(valorCosto, 2);
            }

            // El resto de reglas (precio mayor a cero, nombre repetido, etc.) las valida MenuService
            return true;
        }

        private void BtnAgregarMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCamposMenu(out decimal precio, out decimal? costo)) return;

            var nuevo = new MenuItem
            {
                Nombre = txtMenuNombre.Text.Trim(),
                Categoria = LeerCategoria(),
                Precio = precio,
                Costo = costo,
                Activo = chkMenuActivo.IsOn
            };
            if (!Avisos.Intentar(barraAviso, () => MenuService.Agregar(nuevo))) return;

            RefrescarMenu();
            LimpiarCamposMenu();
            Avisos.Exito(barraAviso, "Producto agregado al menú.");
        }

        private void BtnActualizarMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_menuIdSeleccionado == 0)
            {
                Avisos.Mostrar(barraAviso, "Selecciona un producto de la lista para actualizar.");
                return;
            }
            if (!ValidarCamposMenu(out decimal precio, out decimal? costo)) return;

            var editado = new MenuItem
            {
                Id = _menuIdSeleccionado,
                Nombre = txtMenuNombre.Text.Trim(),
                Categoria = LeerCategoria(),
                Precio = precio,
                Costo = costo,
                Activo = chkMenuActivo.IsOn
            };
            if (!Avisos.Intentar(barraAviso, () => MenuService.Actualizar(editado))) return;

            RefrescarMenu();
            LimpiarCamposMenu();
            Avisos.Exito(barraAviso, "Producto actualizado (el precio/menú se puede cambiar cuando quieras).");
        }

        private async void BtnEliminarMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_menuIdSeleccionado == 0)
            {
                Avisos.Mostrar(barraAviso, "Selecciona un producto de la lista para eliminar.");
                return;
            }

            bool confirmar = await Avisos.ConfirmarAsync(XamlRoot, "Confirmar", "¿Eliminar este producto del menú?");
            if (confirmar)
            {
                int idEliminar = _menuIdSeleccionado;
                if (!Avisos.Intentar(barraAviso, () => MenuService.Eliminar(idEliminar))) return;
                RefrescarMenu();
                LimpiarCamposMenu();
                Avisos.Exito(barraAviso, "Producto eliminado.");
            }
        }

        private void BtnLimpiarMenu_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCamposMenu();
            barraAviso.IsOpen = false;
        }

        private void LimpiarCamposMenu()
        {
            _cargando = true;
            try
            {
                _menuIdSeleccionado = 0;
                dgvMenu.SelectedItem = null;
                txtMenuNombre.Text = string.Empty;
                EscribirCategoria(string.Empty);
                txtMenuPrecio.Text = string.Empty;
                txtMenuCosto.Text = string.Empty;
                chkMenuActivo.IsOn = true;
                lblModo.Text = "Nuevo producto";
            }
            finally { _cargando = false; }
        }
    }
}