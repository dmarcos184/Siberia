using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SiberiaApp.Models
{
    /// <summary>
    /// Corte de caja de UN usuario en UN dia laborado.
    /// Un dia sin corte guardado = dia pendiente de cerrar.
    /// </summary>
    public class Corte
    {
        public const string Abierto = "Abierto";
        public const string Cerrado = "Cerrado";

        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;

        /// <summary>Dia laborado que se esta cortando (solo la fecha).</summary>
        public DateTime Fecha { get; set; }

        public string Estado { get; set; } = Abierto;

        /// <summary>Pedidos validos (no cancelados).</summary>
        public int TotalPedidos { get; set; }
        public int PedidosCancelados { get; set; }

        /// <summary>Suma de todos los pedidos registrados ese dia (incluye los cancelados).</summary>
        public decimal TotalVentas { get; set; }

        /// <summary>Importe de los pedidos cancelados (no es ingreso).</summary>
        public decimal MontoCancelado { get; set; }

        /// <summary>Descuentos dados en pedidos validos (ya vienen restados de las ventas; es informativo).</summary>
        public decimal TotalDescuentos { get; set; }

        /// <summary>Dinero devuelto a clientes de pedidos validos.</summary>
        public decimal TotalDevoluciones { get; set; }

        /// <summary>Ingreso real del dia = ventas - cancelaciones - devoluciones.</summary>
        public decimal TotalNeto { get; set; }

        // Ingreso neto repartido por forma de pago (la suma de las cuatro = TotalNeto)
        public decimal VentasEfectivo { get; set; }
        public decimal VentasTarjeta { get; set; }
        public decimal VentasTransferencia { get; set; }

        /// <summary>Pedidos de versiones anteriores que no tenian metodo de pago.</summary>
        public decimal VentasSinMetodo { get; set; }

        /// <summary>Costo de los pedidos validos que tienen todos sus costos capturados.</summary>
        public decimal Costo { get; set; }
        public decimal VentasConCosto { get; set; }
        public int PedidosConCosto { get; set; }

        public List<int> PedidoIds { get; set; } = new List<int>();
        public string Observaciones { get; set; } = string.Empty;

        public DateTime? FechaCierre { get; set; }
        public string CerradoPor { get; set; } = string.Empty;

        // Auditoria cuando un administrador reabre un corte
        public string ReabiertoPor { get; set; } = string.Empty;
        public DateTime? FechaReapertura { get; set; }
        public string MotivoReapertura { get; set; } = string.Empty;

        [JsonIgnore]
        public bool EstaCerrado => Estado == Cerrado;

        [JsonIgnore]
        public bool HayUtilidad => PedidosConCosto > 0;

        [JsonIgnore]
        public decimal Utilidad => VentasConCosto - Costo;
    }
}
