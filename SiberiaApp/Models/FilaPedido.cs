using System.Linq;
using SiberiaApp.Helpers;

namespace SiberiaApp.Models
{
    /// <summary>
    /// Un pedido ya "formateado" como texto para mostrarlo en una tabla (ListView).
    /// Antes en WinForms esto se hacia con un objeto anonimo (new { p.Folio, ... }),
    /// pero en WinUI el XAML necesita una clase con nombre para poder enlazarla.
    /// No se guarda en el JSON, solo sirve para la pantalla.
    /// </summary>
    public class FilaPedido
    {
        public int Id { get; set; }
        public string Folio { get; set; }
        public string Cliente { get; set; }
        public string TipoPedido { get; set; }
        public string Fecha { get; set; }
        public string Productos { get; set; }
        public string Total { get; set; }
        public string Estado { get; set; }
        public string Usuario { get; set; }

        public static FilaPedido Desde(Pedido p) => new FilaPedido
        {
            Id = p.Id,
            Folio = p.Folio,
            Cliente = p.Cliente,
            TipoPedido = p.TipoPedido,
            Fecha = p.FechaHora.ToString("dd/MM/yyyy HH:mm"),
            // Las notas tambien se ven aqui, ej: "4x Tostada (1 sin crema)"
            Productos = string.Join(", ", p.Detalles.Select(d =>
                d.TieneNotas
                    ? $"{d.Cantidad}x {d.NombreProducto} ({d.ResumenNotas})"
                    : $"{d.Cantidad}x {d.NombreProducto}")),
            Total = Formato.Dinero(p.Total),
            Estado = p.Estado,
            Usuario = string.IsNullOrWhiteSpace(p.UsuarioCreo) ? "(sin usuario)" : p.UsuarioCreo
        };
    }
}
