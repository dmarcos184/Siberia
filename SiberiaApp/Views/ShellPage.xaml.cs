using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SiberiaApp.Services;

namespace SiberiaApp.Views
{
    /// <summary>
    /// Pantalla principal con el menu lateral (antes: MainForm con su TabControl).
    /// Cada opcion del menu abre una pagina distinta dentro de ContentFrame.
    /// Las opciones de administracion solo aparecen (y solo abren) para el rol Administrador.
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        // Pantallas que solo puede abrir un administrador
        private static readonly HashSet<string> SoloAdministrador = new HashSet<string> { "Panel", "Menu", "Usuarios" };

        private readonly bool _esAdmin;

        public ShellPage()
        {
            InitializeComponent();

            var u = SesionService.Actual;
            _esAdmin = u.EsAdministrador;

            App.Ventana.CambiarTitulo($"Siberia - Sistema de Pedidos  |  Usuario: {u.NombreUsuario} ({u.Rol})");
            txtNombreNav.Text = u.NombreUsuario;
            txtRolNav.Text = u.Rol;
            fotoUsuario.DisplayName = u.NombreUsuario;
            itemCerrarSesion.Content = $"Cerrar sesión ({u.NombreUsuario})";

            // Un usuario normal no ve las opciones de administracion
            var visibilidad = _esAdmin ? Visibility.Visible : Visibility.Collapsed;
            itemPanel.Visibility = visibilidad;
            itemMenu.Visibility = visibilidad;
            itemUsuarios.Visibility = visibilidad;
            separadorAdmin.Visibility = visibilidad;

            // Abre la primera opcion al entrar: el panel (administrador) o "Nueva orden"
            Loaded += (s, e) =>
            {
                if (NavView.SelectedItem == null)
                    NavView.SelectedItem = _esAdmin ? itemPanel : itemNuevaOrden;
            };
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = args.SelectedItem as NavigationViewItem ?? args.SelectedItemContainer;
            if (item == null) return;

            string etiqueta = item.Tag as string;

            // Segunda barrera: aunque alguien lograra seleccionar la opcion, no se abre
            if (etiqueta != null && SoloAdministrador.Contains(etiqueta) && !_esAdmin)
                return;

            Type pagina = etiqueta switch
            {
                "Panel" => typeof(DashboardPage),
                "NuevaOrden" => typeof(NuevaOrdenPage),
                "Menu" => typeof(MenuPage),
                "Consultar" => typeof(ConsultarPedidosPage),
                "Actualizar" => typeof(ActualizarPedidoPage),
                "Cortes" => typeof(CortesPage),
                "Reportes" => typeof(ReportesPage),
                "Usuarios" => typeof(UsuariosPage),
                _ => null
            };

            if (pagina != null && ContentFrame.CurrentSourcePageType != pagina)
            {
                ContentFrame.Navigate(pagina, null, new EntranceNavigationTransitionInfo());
                NavView.Header = item.Content;
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer?.Tag as string == "Salir")
            {
                SesionService.Cerrar();
                App.Ventana.CambiarTitulo("Siberia - Sistema de Pedidos");
                App.Ventana.IrA(typeof(LoginPage));
            }
        }
    }
}
