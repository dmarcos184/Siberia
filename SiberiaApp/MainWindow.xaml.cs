using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SiberiaApp.Views;
using Windows.Graphics;

namespace SiberiaApp
{
    /// <summary>
    /// Unica ventana de la app. Adentro tiene un Frame que muestra
    /// primero el Login y despues la pantalla principal (ShellPage).
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Tamaño inicial de la ventana (en pixeles "normales", sin escala)
        private const int AnchoInicial = 1200;
        private const int AltoInicial = 760;

        public MainWindow()
        {
            InitializeComponent();

            // Usa nuestra barra de titulo (la de arriba con el icono)
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(BarraTitulo);

            AjustarTamanoYCentrar();

            // Lo primero que se ve es el login
            FramePrincipal.Navigate(typeof(LoginPage));
        }

        /// <summary>Cambia de pantalla completa (Login &lt;-&gt; Shell).</summary>
        public void IrA(Type pagina)
        {
            FramePrincipal.Navigate(pagina);
            FramePrincipal.BackStack.Clear();
        }

        /// <summary>Cambia el texto de la barra de titulo.</summary>
        public void CambiarTitulo(string titulo)
        {
            Title = titulo;
            TxtTitulo.Text = titulo;
        }

        // ---------------------------------------------------------------
        // Tamaño y posicion de la ventana
        // ---------------------------------------------------------------
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private void AjustarTamanoYCentrar()
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            double escala = GetDpiForWindow(hwnd) / 96.0;

            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;

            int ancho = Math.Min((int)(AnchoInicial * escala), area.Width);
            int alto = Math.Min((int)(AltoInicial * escala), area.Height);

            AppWindow.Resize(new SizeInt32(ancho, alto));
            AppWindow.Move(new PointInt32(
                area.X + (area.Width - ancho) / 2,
                area.Y + (area.Height - alto) / 2));
        }
    }
}
