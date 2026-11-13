using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SiberiaApp.Helpers;
using SiberiaApp.Models;
using SiberiaApp.Services;

namespace SiberiaApp.Views
{
    /// <summary>
    /// Administracion de usuarios (solo administrador): crear, editar, activar/desactivar,
    /// restablecer contraseña y eliminar (solo si nunca registraron nada).
    /// </summary>
    public sealed partial class UsuariosPage : Page
    {
        private int _usuarioIdSeleccionado = 0; // 0 = se esta creando uno nuevo
        private bool _cargando;

        public UsuariosPage()
        {
            InitializeComponent();

            CamposTexto.SinEspaciosRepetidos(txtNuevoUsuario);
            CamposTexto.SinEspacios(txtNuevaPassword);
            ActualizarModo();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefrescarListaUsuarios();
        }

        private void RefrescarListaUsuarios()
        {
            List<Usuario> usuarios = null;
            if (!Avisos.Intentar(barraAviso, () => usuarios = UsuarioService.ObtenerTodos())) return;

            lstUsuarios.ItemsSource = usuarios;
            LimpiarFormulario();
        }

        // ---------------------------------------------------------------
        // Modo del formulario: crear o editar
        // ---------------------------------------------------------------
        private void ActualizarModo()
        {
            bool editando = _usuarioIdSeleccionado != 0;

            lblModoUsuario.Text = editando ? $"Editando: {txtNuevoUsuario.Text}" : "Nuevo usuario";
            txtNuevaPassword.Visibility = editando ? Visibility.Collapsed : Visibility.Visible;
            btnCrearUsuario.Visibility = editando ? Visibility.Collapsed : Visibility.Visible;
            btnGuardarUsuario.Visibility = editando ? Visibility.Visible : Visibility.Collapsed;
            panelAccionesUsuario.Visibility = editando ? Visibility.Visible : Visibility.Collapsed;
            btnNuevoUsuario.Visibility = editando ? Visibility.Visible : Visibility.Collapsed;

            lblAyudaUsuario.Text = editando
                ? "Una cuenta desactivada no puede entrar, pero conserva su historial de pedidos y cortes."
                : "La persona deberá cambiar esta contraseña temporal la primera vez que entre.";
        }

        private void LimpiarFormulario()
        {
            _cargando = true;
            try
            {
                _usuarioIdSeleccionado = 0;
                lstUsuarios.SelectedItem = null;
                txtNuevoUsuario.Text = string.Empty;
                txtNuevaPassword.Password = string.Empty;
                cmbNuevoRol.SelectedIndex = 0;
                chkUsuarioActivo.IsOn = true;
            }
            finally { _cargando = false; }
            ActualizarModo();
        }

        private void LstUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargando || lstUsuarios.SelectedItem is not Usuario usuario) return;

            _usuarioIdSeleccionado = usuario.Id;
            txtNuevoUsuario.Text = usuario.NombreUsuario;
            cmbNuevoRol.SelectedItem = usuario.Rol;
            chkUsuarioActivo.IsOn = usuario.Activo;
            ActualizarModo();
        }

        private void BtnNuevoUsuario_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormulario();
            barraAviso.IsOpen = false;
        }

        // ---------------------------------------------------------------
        // Acciones (cada una la vuelve a validar UsuarioService: rol, reglas y datos)
        // ---------------------------------------------------------------
        private void BtnCrearUsuario_Click(object sender, RoutedEventArgs e)
        {
            string nombre = txtNuevoUsuario.Text;
            string password = txtNuevaPassword.Password;
            string rol = cmbNuevoRol.SelectedItem as string ?? Roles.Cajero;

            if (!Avisos.Intentar(barraAviso, () => UsuarioService.Crear(nombre, password, rol))) return;

            RefrescarListaUsuarios();
            Avisos.Exito(barraAviso, "Usuario creado correctamente.");
        }

        private async void BtnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioIdSeleccionado == 0) return;

            int id = _usuarioIdSeleccionado;
            string nombre = txtNuevoUsuario.Text;
            string rol = cmbNuevoRol.SelectedItem as string ?? Roles.Cajero;
            bool activo = chkUsuarioActivo.IsOn;

            // Desactivar una cuenta o cambiar un rol son cambios importantes: se confirman
            var actual = lstUsuarios.SelectedItem as Usuario;
            if (actual != null && (actual.Activo && !activo || actual.Rol != rol))
            {
                string detalle = actual.Activo && !activo
                    ? $"¿Desactivar la cuenta de \"{actual.NombreUsuario}\"? Ya no podrá iniciar sesión."
                    : $"¿Cambiar el rol de \"{actual.NombreUsuario}\" a {rol}?";
                if (!await Avisos.ConfirmarAsync(XamlRoot, "Confirmar cambio", detalle)) return;
            }

            if (!Avisos.Intentar(barraAviso, () => UsuarioService.Actualizar(id, nombre, rol, activo))) return;

            RefrescarListaUsuarios();
            Avisos.Exito(barraAviso, "Usuario actualizado.");
        }

        private async void BtnRestablecer_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioIdSeleccionado == 0) return;
            int id = _usuarioIdSeleccionado;
            string nombre = (lstUsuarios.SelectedItem as Usuario)?.NombreUsuario ?? string.Empty;

            bool aplicado = await PasswordDialog.PedirYAplicarAsync(
                XamlRoot,
                $"Restablecer contraseña de {nombre}",
                "Escribe una contraseña temporal. La persona deberá cambiarla al iniciar sesión.",
                "Cancelar",
                temporal => UsuarioService.RestablecerPassword(id, temporal));

            if (aplicado)
            {
                RefrescarListaUsuarios();
                Avisos.Exito(barraAviso, $"Contraseña de \"{nombre}\" restablecida. Deberá cambiarla al entrar.");
            }
        }

        private async void BtnEliminarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioIdSeleccionado == 0) return;
            int id = _usuarioIdSeleccionado;
            string nombre = (lstUsuarios.SelectedItem as Usuario)?.NombreUsuario ?? string.Empty;

            bool confirmar = await Avisos.ConfirmarAsync(XamlRoot, "Eliminar usuario",
                $"¿Eliminar a \"{nombre}\"? Solo se puede si nunca registró pedidos ni cortes; " +
                "si ya tiene historial, desactívalo en su lugar.");
            if (!confirmar) return;

            if (!Avisos.Intentar(barraAviso, () => UsuarioService.Eliminar(id))) return;

            RefrescarListaUsuarios();
            Avisos.Exito(barraAviso, "Usuario eliminado.");
        }
    }
}
