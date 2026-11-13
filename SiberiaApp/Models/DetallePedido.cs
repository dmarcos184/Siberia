using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SiberiaApp.Models
{
    public class DetallePedido
    {
        public int MenuItemId { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public int Cantidad { get; set; }

        /// <summary>
        /// Costo unitario que tenia el producto al momento de la venta (null si no estaba capturado).
        /// Se guarda aqui para que cambiar el costo del menu no altere las ganancias historicas.
        /// </summary>
        public decimal? CostoUnitario { get; set; }

        /// <summary>Observaciones de este producto (ej. "1 sin crema", "2 con extra de aguacate").</summary>
        public List<NotaProducto> Notas { get; set; } = new List<NotaProducto>();

        public decimal Subtotal => Math.Round(PrecioUnitario * Cantidad, 2);

        [JsonIgnore]
        public bool TieneNotas => Notas != null && Notas.Count > 0;

        /// <summary>Texto corto con todas las notas, para mostrarlo debajo del producto.</summary>
        [JsonIgnore]
        public string ResumenNotas => TieneNotas
            ? string.Join("  ·  ", Notas.Select(n => n.Descripcion))
            : string.Empty;
    }
}
