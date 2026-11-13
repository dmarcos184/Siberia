using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SiberiaApp.Services;
using SiberiaApp.Views;

namespace SiberiaApp.Helpers
{
    /// <summary>
    /// Reemplazo de MessageBox.Show de WinForms.
    /// - Mostrar(...)        -> muestra un InfoBar (barra de aviso arriba de la pagina)
    /// - ConfirmarAsync(...) -> muestra un ContentDialog con botones Si / No
    /// - Intentar(...)       -> ejecuta una accion y convierte las reglas de negocio rotas en un aviso claro
    /// </summary>
    public static class Avisos
    {
        public static void Mostrar(InfoBar barra, string mensaje, InfoBarSeverity tipo = InfoBarSeverity.Warning)
        {
            barra.Title = tipo switch
            {
                InfoBarSeverity.Success => "Listo",
                InfoBarSeverity.Error => "Error",
                InfoBarSeverity.Informational => "Información",
                _ => "Aviso"
            };
            barra.Message = mensaje;
            barra.Severity = tipo;
            barra.IsOpen = true;
        }

        public static void Exito(InfoBar barra, string mensaje) =>
            Mostrar(barra, mensaje, InfoBarSeverity.Success);

        /// <summary>
        /// Muestra el aviso que corresponde a una excepcion de reglas del negocio.
        /// Devuelve false si la excepcion NO es de las esperadas (para que se propague y quede en error.log).
        /// </summary>
        public static bool MostrarError(InfoBar barra, Exception ex)
        {
            switch (ex)
            {
                case ReglaNegocioException:
                    Mostrar(barra, ex.Message, InfoBarSeverity.Warning);
                    SalirSiSesionTerminada();
                    return true;
                case DatosCorruptosException:
                    Mostrar(barra, ex.Message, InfoBarSeverity.Error);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Ejecuta la accion; si una regla del negocio lo impide, muestra el mensaje y devuelve false.</summary>
        public static bool Intentar(InfoBar barra, Action accion)
        {
            try
            {
                accion();
                return true;
            }
            catch (Exception ex) when (ex is ReglaNegocioException || ex is DatosCorruptosException)
            {
                MostrarError(barra, ex);
                return false;
            }
        }

        // Si la cuenta se desactivo o cerro mientras la persona usaba la app, se regresa al login.
        private static void SalirSiSesionTerminada()
        {
            if (SesionService.Actual == null && App.Ventana != null)
                App.Ventana.IrA(typeof(LoginPage));
        }

        public static async Task<bool> ConfirmarAsync(XamlRoot xamlRoot, string titulo, string mensaje)
        {
            var dialogo = new ContentDialog
            {
                Title = titulo,
                Content = new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Sí",
                CloseButtonText = "No",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
            };
            return await dialogo.ShowAsync() == ContentDialogResult.Primary;
        }

        /// <summary>Pide un texto (ej. el motivo de reabrir un corte). Devuelve null si cancelan.</summary>
        public static async Task<string> PedirTextoAsync(XamlRoot xamlRoot, string titulo, string mensaje,
                                                         string textoEjemplo, int longitudMaxima)
        {
            var caja = new TextBox
            {
                PlaceholderText = textoEjemplo,
                MaxLength = longitudMaxima,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 80
            };
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(caja);

            var dialogo = new ContentDialog
            {
                Title = titulo,
                Content = panel,
                PrimaryButtonText = "Aceptar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
            };
            return await dialogo.ShowAsync() == ContentDialogResult.Primary ? caja.Text : null;
        }
    }
}
