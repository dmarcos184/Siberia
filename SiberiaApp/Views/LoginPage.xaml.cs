using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SiberiaApp.Helpers;
using SiberiaApp.Models;
using SiberiaApp.Services;
using Windows.System;

namespace SiberiaApp.Views
{
    /// <summary>Pantalla de inicio de sesion (antes: LoginForm).</summary>
    public sealed partial class LoginPage : Page
    {
        private bool _ingresando;

        public LoginPage()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                txtUsuario.Focus(FocusState.Programmatic);

                // Si al abrir la app fallo algo con los datos, se avisa aqui
                if (App.ErrorInicio != null)
                    MostrarMensaje("No se pudieron abrir los datos", App.ErrorInicio, InfoBarSeverity.Error);
            };

            CamposTexto.SinEspaciosRepetidos(txtUsuario);
            CamposTexto.SinEspacios(txtPassword);
        }

        private async void BtnIngresar_Click(object sender, RoutedEventArgs e) => await IntentarLoginAsync();

        // Enter en la contraseña = clic en "Ingresar" (antes: AcceptButton)
        private async void TxtPassword_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                await IntentarLoginAsync();
        }

        private void MostrarMensaje(string titulo, string mensaje, InfoBarSeverity tipo)
        {
            lblMensaje.Title = titulo;
            lblMensaje.Message = mensaje;
            lblMensaje.Severity = tipo;
            lblMensaje.IsOpen = true;
        }

        private void PonerCargando(bool cargando)
        {
            _ingresando = cargando;
            btnIngresar.IsEnabled = !cargando;
            anilloCarga.IsActive = cargando;
            anilloCarga.Visibility = cargando ? Visibility.Visible : Visibility.Collapsed;
            txtBotonIngresar.Text = cargando ? "Entrando..." : "Ingresar";
        }

        private async Task IntentarLoginAsync()
        {
            if (_ingresando) return; // evita doble clic / doble Enter

            lblMensaje.IsOpen = false;
            string usuarioTexto = txtUsuario.Text.Trim();
            string password = txtPassword.Password;
            PonerCargando(true);

            try
            {
                // Comprobar la contraseña es lento a proposito (PBKDF2): se hace fuera del hilo de la pantalla
                Usuario usuario = await Task.Run(() => UsuarioService.Autenticar(usuarioTexto, password));

                if (usuario == null)
                {
                    MostrarMensaje("No se pudo iniciar sesión", "Usuario o contraseña incorrectos.", InfoBarSeverity.Error);
                    txtPassword.Password = string.Empty;
                    txtPassword.Focus(FocusState.Programmatic);
                    return;
                }

                SesionService.Iniciar(usuario);

                // Cuenta nueva o con contraseña restablecida: debe elegir una propia antes de entrar
                if (usuario.DebeCambiarPassword)
                {
                    bool cambio = await PasswordDialog.PedirYAplicarAsync(
                        XamlRoot,
                        "Elige tu contraseña",
                        "Tu cuenta usa una contraseña temporal. Escribe una contraseña nueva para continuar.",
                        "Cancelar",
                        nueva => UsuarioService.EstablecerPasswordPropia(nueva));

                    if (!cambio)
                    {
                        SesionService.Cerrar();
                        txtPassword.Password = string.Empty;
                        return;
                    }
                }

                App.Ventana.IrA(typeof(ShellPage));
            }
            catch (Exception ex) when (ex is ReglaNegocioException || ex is DatosCorruptosException)
            {
                SesionService.Cerrar();
                MostrarMensaje("No se pudo iniciar sesión", ex.Message,
                    ex is DatosCorruptosException ? InfoBarSeverity.Error : InfoBarSeverity.Warning);
                txtPassword.Password = string.Empty;
            }
            finally
            {
                PonerCargando(false);
            }
        }
    }
}
