using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SiberiaApp.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public string Folio { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;

        // "Mostrador" (compra en fisico) o "Domicilio"
        public string TipoPedido { get; set; } = "Mostrador";
        public string Direccion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;

        public DateTime FechaHora { get; set; } = DateTime.Now;
        public List<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();

        // "Pendiente" / "En preparacion" / "Cancelado" / "Entregado"
        public string Estado { get; set; } = EstadosPedido.Pendiente;

        /// <summary>Nombre de quien lo capturo (copia del momento; se conserva aunque borren o renombren al usuario).</summary>
        public string UsuarioCreo { get; set; } = string.Empty;

        /// <summary>Id del usuario que lo capturo. 0 = pedido anterior que no se pudo ligar a un usuario.</summary>
        public int UsuarioId { get; set; }

        /// <summary>Id del corte de caja donde quedo incluido (0 = todavia sin corte).</summary>
        public int CorteId { get; set; }

        // ---------- Pago, descuento y devolucion ----------

        /// <summary>Efectivo / Tarjeta / Transferencia. Vacio en pedidos de versiones anteriores.</summary>
        public string MetodoPago { get; set; } = string.Empty;

        /// <summary>Descuento en pesos (ya restado del total). 0 si no hubo.</summary>
        public decimal Descuento { get; set; }

        /// <summary>Dinero devuelto al cliente despues de entregar (acumulado). 0 si no hubo.</summary>
        public decimal MontoDevuelto { get; set; }
        public string MotivoDevolucion { get; set; } = string.Empty;
        public string DevueltoPor { get; set; } = string.Empty;
        public DateTime? FechaDevolucion { get; set; }

        /// <summary>Suma de los productos, antes de descuento.</summary>
        public decimal Subtotal => Detalles == null ? 0m : Detalles.Sum(d => d.Subtotal);

        /// <summary>Lo que se cobra: productos menos descuento.</summary>
        public decimal Total => Subtotal - Descuento;

        /// <summary>Lo que realmente se quedo el negocio: lo cobrado menos lo devuelto.</summary>
        [JsonIgnore]
        public decimal TotalNeto => Total - MontoDevuelto;

        [JsonIgnore]
        public string MetodoPagoTexto => MetodosPago.Nombre(MetodoPago);

        [JsonIgnore]
        public bool EsFinal => EstadosPedido.EsFinal(Estado);

        [JsonIgnore]
        public bool EsCancelado => Estado == EstadosPedido.Cancelado;

        /// <summary>
        /// Costo total del pedido, o null si algun producto no tenia costo capturado
        /// (en ese caso la ganancia de este pedido no se puede calcular).
        /// </summary>
        [JsonIgnore]
        public decimal? CostoTotal
        {
            get
            {
                if (Detalles == null || Detalles.Count == 0) return null;
                if (Detalles.Any(d => d.CostoUnitario == null)) return null;
                return Detalles.Sum(d => d.CostoUnitario.Value * d.Cantidad);
            }
        }
    }
}
