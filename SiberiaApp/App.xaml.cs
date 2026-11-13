using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SiberiaApp.Models;
using SiberiaApp.Services;

namespace SiberiaApp
{
    /// <summary>
    /// Punto de entrada de la app (reemplaza a Program.cs de WinForms).
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Usuario que inicio sesion. Se guarda en SesionService (ahi se valida cada operacion);
        /// esta propiedad se conserva para no romper codigo que ya la usaba.
        /// </summary>
        public static Usuario UsuarioActual
        {
            get => SesionService.Actual;
            set
            {
                if (value == null) SesionService.Cerrar();
                else SesionService.Iniciar(value);
            }
        }

        /// <summary>La ventana principal, para poder navegar desde cualquier pagina.</summary>
        public static MainWindow Ventana { get; private set; }

        /// <summary>Si algo fallo al abrir los datos, aqui queda el mensaje para mostrarlo en el login.</summary>
        public static string ErrorInicio { get; private set; }

        /// <summary>Archivo donde se guardan los errores (junto al .exe).</summary>
        public static readonly string ArchivoErrores =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");

        public App()
        {
            InitializeComponent();

            // Si algo truena en una pantalla, en vez de cerrarse la app
            // se muestra el error y se guarda en error.log
            UnhandledException += App_UnhandledException;

            // Errores fuera de las pantallas: solo se guardan en error.log
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                GuardarError(e.ExceptionObject?.ToString() ?? "Error desconocido");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Actualiza los .json de versiones anteriores (contraseñas, pedidos viejos) sin perder datos
            try
            {
                MigracionDatos.Ejecutar();
            }
            catch (Exception ex)
            {
                ErrorInicio = ex.Message;
                GuardarError(ex.ToString());
            }

            Ventana = new MainWindow();
            Ventana.Activate();
        }

        // ---------------------------------------------------------------
        // Manejo de errores
        // ---------------------------------------------------------------
        private async void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true; // evita que la app se cierre

            string detalle = $"{e.Message}\r\n\r\n{e.Exception}";
            GuardarError(detalle);

            try
            {
                var root = Ventana?.Content?.XamlRoot;
                if (root == null) return;

                var dialogo = new ContentDialog
                {
                    Title = "Ocurrió un error",
                    Content = new ScrollViewer
                    {
                        MaxHeight = 400,
                        Content = new TextBlock
                        {
                            Text = "Se guardó en: " + ArchivoErrores + "\r\n\r\n" + detalle,
                            TextWrapping = TextWrapping.Wrap,
                            IsTextSelectionEnabled = true
                        }
                    },
                    CloseButtonText = "Cerrar",
                    XamlRoot = root
                };
                await dialogo.ShowAsync();
            }
            catch
            {
                // si ni siquiera se puede mostrar el dialogo, ya quedo en error.log
            }
        }

        private static void GuardarError(string detalle)
        {
            try
            {
                File.AppendAllText(ArchivoErrores,
                    $"===== {DateTime.Now:dd/MM/yyyy HH:mm:ss} =====\r\n{detalle}\r\n\r\n");
            }
            catch
            {
                // no hacemos nada si no se puede escribir el log
            }
        }
    }
}
