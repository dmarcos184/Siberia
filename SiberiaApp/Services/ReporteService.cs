using System;
using System.Collections.Generic;
using System.Linq;
using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    public class ProductoVendido
    {
        public string Nombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal Importe { get; set; }
    }

    public class VentaDiaria
    {
        public DateTime Fecha { get; set; }
        public int Pedidos { get; set; }
        public decimal Ventas { get; set; }
    }

    /// <summary>
    /// Resumen de un periodo. Distingue:
    ///  - Ventas: lo vendido en pedidos NO cancelados (ingreso).
    ///  - Costo / Utilidad: solo con los pedidos que tienen costo capturado; si no hay, NO se calcula.
    /// </summary>
    public class ResumenVentas
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }

        public int Pedidos { get; set; }
        public int Cancelados { get; set; }
        public decimal Ventas { get; set; }
        public decimal MontoCancelado { get; set; }

        public int PedidosConCosto { get; set; }
        public decimal VentasConCosto { get; set; }
        public decimal Costo { get; set; }

        /// <summary>Descuentos dados (ya restados de Ventas) y dinero devuelto (ya restado de Ventas).</summary>
        public decimal Descuentos { get; set; }
        public decimal Devoluciones { get; set; }

        // Ventas netas por forma de pago (la suma de las cuatro = Ventas)
        public decimal VentasEfectivo { get; set; }
        public decimal VentasTarjeta { get; set; }
        public decimal VentasTransferencia { get; set; }
        public decimal VentasSinMetodo { get; set; }

        public List<ProductoVendido> TopProductos { get; set; } = new List<ProductoVendido>();
        public List<VentaDiaria> PorDia { get; set; } = new List<VentaDiaria>();

        /// <summary>Texto corto para la pantalla: "Efectivo $x · Tarjeta $y · Transferencia $z".</summary>
        public string TextoPorMetodo
        {
            get
            {
                string texto = $"Efectivo {VentasEfectivo:C2}  ·  Tarjeta {VentasTarjeta:C2}  ·  Transferencia {VentasTransferencia:C2}";
                if (VentasSinMetodo != 0) texto += $"  ·  Sin registrar {VentasSinMetodo:C2}";
                return texto;
            }
        }

        public decimal TicketPromedio => Pedidos == 0 ? 0m : Math.Round(Ventas / Pedidos, 2);
        public int PedidosSinCosto => Pedidos - PedidosConCosto;

        /// <summary>true si hay al menos un pedido con costo para poder calcular utilidad.</summary>
        public bool HayUtilidad => PedidosConCosto > 0;
        public decimal Utilidad => VentasConCosto - Costo;

        /// <summary>true si TODOS los pedidos del periodo tienen costo (la utilidad es completa).</summary>
        public bool UtilidadCompleta => Pedidos > 0 && PedidosSinCosto == 0;

        /// <summary>Texto para la pantalla que explica de donde sale (o por que falta) la utilidad.</summary>
        public string NotaUtilidad
        {
            get
            {
                if (Pedidos == 0) return "Sin pedidos en el periodo.";
                if (!HayUtilidad) return "No calculable: ningún producto vendido tiene costo capturado (captúralo en Menú y precios).";
                if (UtilidadCompleta) return "Calculada con el costo de todos los pedidos.";
                return $"Parcial: solo {PedidosConCosto} de {Pedidos} pedidos tienen costo capturado.";
            }
        }
    }

    public class ComparacionPeriodos
    {
        public ResumenVentas Actual { get; set; }
        public ResumenVentas Anterior { get; set; }

        /// <summary>Cambio porcentual de ventas vs. el periodo anterior (null si el anterior no tuvo ventas).</summary>
        public decimal? VariacionVentas =>
            Anterior == null || Anterior.Ventas == 0
                ? (decimal?)null
                : Math.Round((Actual.Ventas - Anterior.Ventas) / Anterior.Ventas * 100m, 1);
    }

    public class PanelAdmin
    {
        public ComparacionPeriodos Hoy { get; set; }
        public ComparacionPeriodos Semana { get; set; }
        public ComparacionPeriodos Mes { get; set; }
        public int UsuariosActivos { get; set; }
        public int CortesPendientes { get; set; }
        public int PedidosEnCurso { get; set; }
        public List<ProductoVendido> TopProductosMes { get; set; } = new List<ProductoVendido>();
        public List<VentaDiaria> UltimosSieteDias { get; set; } = new List<VentaDiaria>();
    }

    /// <summary>
    /// Ventas y ganancias a partir de los pedidos REALES. Respeta permisos:
    /// el usuario normal solo obtiene numeros de sus propios pedidos; el panel general es solo de administrador.
    /// </summary>
    public static class ReporteService
    {
        private static ResumenVentas Resumir(IEnumerable<Pedido> origen, DateTime desde, DateTime hasta)
        {
            var delPeriodo = origen
                .Where(p => p.FechaHora.Date >= desde.Date && p.FechaHora.Date <= hasta.Date)
                .ToList();
            var validos = delPeriodo.Where(p => !p.EsCancelado).ToList();
            var cancelados = delPeriodo.Where(p => p.EsCancelado).ToList();

            var resumen = new ResumenVentas
            {
                Desde = desde.Date,
                Hasta = hasta.Date,
                Pedidos = validos.Count,
                Cancelados = cancelados.Count,
                // Ventas = lo cobrado (ya con descuento) menos lo devuelto, sin contar cancelados
                Ventas = validos.Sum(p => p.TotalNeto),
                MontoCancelado = cancelados.Sum(p => p.Total),
                Descuentos = validos.Sum(p => p.Descuento),
                Devoluciones = validos.Sum(p => p.MontoDevuelto)
            };

            foreach (var p in validos)
            {
                switch (p.MetodoPago)
                {
                    case MetodosPago.Efectivo: resumen.VentasEfectivo += p.TotalNeto; break;
                    case MetodosPago.Tarjeta: resumen.VentasTarjeta += p.TotalNeto; break;
                    case MetodosPago.Transferencia: resumen.VentasTransferencia += p.TotalNeto; break;
                    default: resumen.VentasSinMetodo += p.TotalNeto; break;
                }

                decimal? costo = p.CostoTotal;
                if (!costo.HasValue) continue;
                resumen.PedidosConCosto++;
                resumen.VentasConCosto += p.TotalNeto;
                resumen.Costo += costo.Value;
            }

            resumen.TopProductos = validos
                .SelectMany(p => p.Detalles)
                .GroupBy(d => d.MenuItemId)
                .Select(g => new ProductoVendido
                {
                    Nombre = g.Last().NombreProducto,
                    Cantidad = g.Sum(d => d.Cantidad),
                    Importe = g.Sum(d => d.Subtotal)
                })
                .OrderByDescending(x => x.Cantidad)
                .ThenByDescending(x => x.Importe)
                .Take(5)
                .ToList();

            resumen.PorDia = validos
                .GroupBy(p => p.FechaHora.Date)
                .Select(g => new VentaDiaria { Fecha = g.Key, Pedidos = g.Count(), Ventas = g.Sum(p => p.TotalNeto) })
                .OrderBy(v => v.Fecha)
                .ToList();

            return resumen;
        }

        /// <summary>Resumen del periodo. Administrador: todos o un usuario. Usuario normal: solo el suyo.</summary>
        public static ResumenVentas ObtenerResumen(DateTime desde, DateTime hasta, int? usuarioId = null)
        {
            if (hasta.Date < desde.Date)
                throw new ReglaNegocioException("La fecha final no puede ser anterior a la inicial.");

            var pedidos = PedidoService.Consultar(null, desde, hasta, usuarioId);
            return Resumir(pedidos, desde, hasta);
        }

        /// <summary>El periodo elegido contra el periodo anterior de la misma duracion.</summary>
        public static ComparacionPeriodos Comparar(DateTime desde, DateTime hasta, int? usuarioId = null)
        {
            if (hasta.Date < desde.Date)
                throw new ReglaNegocioException("La fecha final no puede ser anterior a la inicial.");

            int dias = (hasta.Date - desde.Date).Days + 1;
            var desdeAnt = desde.Date.AddDays(-dias);
            var hastaAnt = desde.Date.AddDays(-1);

            var pedidos = PedidoService.Consultar(null, desdeAnt, hasta, usuarioId);
            return new ComparacionPeriodos
            {
                Actual = Resumir(pedidos, desde, hasta),
                Anterior = Resumir(pedidos, desdeAnt, hastaAnt)
            };
        }

        /// <summary>Todo lo del panel de administracion. Solo administrador.</summary>
        public static PanelAdmin ObtenerPanelAdmin()
        {
            SesionService.RequerirAdmin();

            var hoy = DateTime.Today;
            var inicioSemana = Periodos.InicioSemana(hoy);
            var inicioMes = Periodos.InicioMes(hoy);

            // Mismos dias transcurridos en el periodo anterior (semana a la fecha vs. semana anterior a la misma fecha)
            var inicioMesAnt = inicioMes.AddMonths(-1);
            var mismoDiaMesAnt = new DateTime(inicioMesAnt.Year, inicioMesAnt.Month,
                Math.Min(hoy.Day, DateTime.DaysInMonth(inicioMesAnt.Year, inicioMesAnt.Month)));

            // Una sola lectura de pedidos (desde el inicio de lo mas antiguo que se necesita)
            var minimo = new[] { inicioMesAnt, inicioSemana.AddDays(-7), hoy.AddDays(-6) }.Min();
            var pedidos = PedidoService.Consultar(null, minimo, hoy);

            ComparacionPeriodos Cmp(DateTime d, DateTime h, DateTime da, DateTime ha) => new ComparacionPeriodos
            {
                Actual = Resumir(pedidos, d, h),
                Anterior = Resumir(pedidos, da, ha)
            };

            var panel = new PanelAdmin
            {
                Hoy = Cmp(hoy, hoy, hoy.AddDays(-1), hoy.AddDays(-1)),
                Semana = Cmp(inicioSemana, hoy, inicioSemana.AddDays(-7), hoy.AddDays(-7)),
                Mes = Cmp(inicioMes, hoy, inicioMesAnt, mismoDiaMesAnt),
                UsuariosActivos = UsuarioService.CargarInterno().Count(u => u.Activo),
                CortesPendientes = CorteService.ContarCortesPendientes(),
                PedidosEnCurso = pedidos.Count(p => !p.EsFinal)
            };

            panel.TopProductosMes = panel.Mes.Actual.TopProductos;

            // Ultimos 7 dias, incluyendo los que no tuvieron ventas (en cero, que es un dato real)
            var porDia = Resumir(pedidos, hoy.AddDays(-6), hoy).PorDia.ToDictionary(v => v.Fecha);
            for (int i = 6; i >= 0; i--)
            {
                var dia = hoy.AddDays(-i);
                panel.UltimosSieteDias.Add(porDia.TryGetValue(dia, out var ventaDelDia)
                    ? ventaDelDia
                    : new VentaDiaria { Fecha = dia, Pedidos = 0, Ventas = 0m });
            }

            return panel;
        }
    }
}
