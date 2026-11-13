using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SiberiaApp.Services;

namespace SiberiaApp.Helpers
{
    /// <summary>
    /// Ventana para escribir una contraseña nueva (con confirmacion).
    /// Se usa al restablecer la contraseña de alguien y cuando una cuenta debe cambiarla al entrar.
    /// </summary>
    public static class PasswordDialog
    {
        /// <summary>
        /// Pide la contraseña y la aplica con <paramref name="aplicar"/>. Si el servicio rechaza la
        /// contraseña (muy corta, igual al usuario...) vuelve a preguntar mostrando el motivo.
        /// Devuelve true si se aplico; false si la persona cancelo.
        /// </summary>
        public static async Task<bool> PedirYAplicarAsync(XamlRoot xamlRoot, string titulo, string mensaje,
                                                          string textoCancelar, Action<string> aplicar)
        {
            string error = null;
            while (true)
            {
                string password = await PedirAsync(xamlRoot, titulo, mensaje, textoCancelar, error);
                if (password == null) return false;

                try
                {
                    aplicar(password);
                    return true;
                }
                catch (ReglaNegocioException ex)
                {
                    error = ex.Message;
                }
            }
        }

        private static async Task<string> PedirAsync(XamlRoot xamlRoot, string titulo, string mensaje,
                                                     string textoCancelar, string error)
        {
            var nueva = new PasswordBox { Header = "Contraseña nueva", PlaceholderText = "Mínimo 6 caracteres, sin espacios" };
            var confirmar = new PasswordBox { Header = "Confirmar contraseña" };
            CamposTexto.SinEspacios(nueva);
            CamposTexto.SinEspacios(confirmar);

            var aviso = new InfoBar
            {
                Severity = InfoBarSeverity.Warning,
                IsClosable = false,
                IsOpen = error != null,
                Message = error ?? string.Empty
            };

            var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
            panel.Children.Add(new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(nueva);
            panel.Children.Add(confirmar);
            panel.Children.Add(aviso);

            var dialogo = new ContentDialog
            {
                Title = titulo,
                Content = panel,
                PrimaryButtonText = "Guardar",
                CloseButtonText = textoCancelar,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
            };

            // Las dos cajas deben coincidir; si no, la ventana no se cierra
            dialogo.PrimaryButtonClick += (s, args) =>
            {
                if (nueva.Password != confirmar.Password)
                {
                    aviso.Message = "Las contraseñas no coinciden.";
                    aviso.IsOpen = true;
                    args.Cancel = true;
                }
            };

            return await dialogo.ShowAsync() == ContentDialogResult.Primary ? nueva.Password : null;
        }
    }
}
