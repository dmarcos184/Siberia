using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Guarda los tickets como archivos .txt en la carpeta "Tickets"
    /// que se crea junto al .exe (igual que la carpeta Data).
    /// Usa el mismo texto que PedidoService.GenerarTicket.
    /// </summary>
    public static class TicketService
    {
        public static readonly string CarpetaTickets =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tickets");

        /// <summary>
        /// Crea el archivo Tickets\FOLIO.txt con el ticket del pedido
        /// y regresa la ruta completa del archivo.
        /// Si ya existia, lo reemplaza (por si cambio el estado, etc.).
        /// </summary>
        public static string GuardarTicketTxt(Pedido pedido)
        {
            if (!Directory.Exists(CarpetaTickets))
                Directory.CreateDirectory(CarpetaTickets);

            string ruta = Path.Combine(CarpetaTickets, $"{pedido.Folio}.txt");
            string texto = PedidoService.GenerarTicket(pedido);

            // UTF8 con BOM para que el Bloc de notas muestre bien acentos y la ¡
            File.WriteAllText(ruta, texto, new UTF8Encoding(true));
            return ruta;
        }

        /// <summary>Abre la carpeta Tickets en el Explorador de Windows.</summary>
        public static void AbrirCarpeta()
        {
            if (!Directory.Exists(CarpetaTickets))
                Directory.CreateDirectory(CarpetaTickets);

            Process.Start(new ProcessStartInfo
            {
                FileName = CarpetaTickets,
                UseShellExecute = true
            });
        }
    }
}
