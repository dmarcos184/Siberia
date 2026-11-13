using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SiberiaApp.Helpers;
using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    /// <summary>Un dia en que un usuario trabajo (tiene pedidos) y todavia no tiene su corte cerrado.</summary>
    public class DiaPorCerrar
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public int Pedidos { get; set; }
        public int SinFinalizar { get; set; }
        public decimal TotalNeto { get; set; }

        public bool ListoParaCerrar => SinFinalizar == 0;
        public bool TieneSinFinalizar => SinFinalizar > 0;
    }

    /// <summary>
    /// Cortes de caja: uno por usuario y por dia laborado.
    ///  - Un usuario normal solo corta SUS dias (y solo dias en que tiene pedidos).
    ///  - Un corte cerrado no se puede duplicar y bloquea los pedidos de ese dia.
    ///  - Solo un administrador consulta todos los cortes o reabre uno (queda registrado quien y por que).
    /// </summary>
    public static class CorteService
    {
        private const string Archivo = "cortes.json";
        public const int LongitudMaximaObservaciones = 300;
        public const int LongitudMinimaMotivo = 5;

        // ---------------------------------------------------------------
        // Acceso interno
        // ---------------------------------------------------------------
        internal static List<Corte> CargarInterno()
        {
            var lista = JsonDataService.Cargar<Corte>(Archivo);
            foreach (var c in lista)
                c.PedidoIds ??= new List<int>();
            return lista;
        }

        internal static bool UsuarioTieneCortes(int usuarioId) =>
            CargarInterno().Any(c => c.UsuarioId == usuarioId);

        /// <summary>true si ese usuario ya cerro su corte de esa fecha.</summary>
        internal static bool DiaCerrado(List<Corte> cortes, int usuarioId, DateTime fecha) =>
            cortes.Any(c => c.EstaCerrado && c.UsuarioId == usuarioId && c.Fecha.Date == fecha.Date);

        /// <summary>true si el pedido pertenece a un dia con corte cerrado (no se debe tocar).</summary>
        internal static bool EstaBloqueado(Pedido pedido, List<Corte> cortes) =>
            pedido.UsuarioId != 0 && DiaCerrado(cortes, pedido.UsuarioId, pedido.FechaHora);

        // ---------------------------------------------------------------
        // Calculo
        // ---------------------------------------------------------------
        private static Corte Calcular(Usuario objetivo, DateTime fecha, List<Pedido> delDia)
        {
            var validos = delDia.Where(p => !p.EsCancelado).ToList();
            var cancelados = delDia.Where(p => p.EsCancelado).ToList();

            var corte = new Corte
            {
                UsuarioId = objetivo.Id,
                NombreUsuario = objetivo.NombreUsuario,
                Fecha = fecha.Date,
                Estado = Corte.Abierto,
                TotalPedidos = validos.Count,
                PedidosCancelados = cancelados.Count,
                TotalVentas = delDia.Sum(p => p.Total),
                MontoCancelado = cancelados.Sum(p => p.Total),
                TotalDescuentos = validos.Sum(p => p.Descuento),
                TotalDevoluciones = validos.Sum(p => p.MontoDevuelto),
                PedidoIds = delDia.Select(p => p.Id).ToList()
            };
            corte.TotalNeto = corte.TotalVentas - corte.MontoCancelado - corte.TotalDevoluciones;

            // Ingreso neto por forma de pago (lo cobrado menos lo devuelto de cada pedido valido).
            // La suma de los cuatro renglones siempre es igual a TotalNeto.
            foreach (var p in validos)
            {
                switch (p.MetodoPago)
                {
                    case MetodosPago.Efectivo: corte.VentasEfectivo += p.TotalNeto; break;
                    case MetodosPago.Tarjeta: corte.VentasTarjeta += p.TotalNeto; break;
                    case MetodosPago.Transferencia: corte.VentasTransferencia += p.TotalNeto; break;
                    default: corte.VentasSinMetodo += p.TotalNeto; break; // pedidos de versiones anteriores
                }
            }

            // Costos: solo de pedidos donde TODOS los productos tienen costo capturado. No se inventa nada.
            foreach (var p in validos)
            {
                decimal? costo = p.CostoTotal;
                if (!costo.HasValue) continue;
                corte.Costo += costo.Value;
                corte.VentasConCosto += p.TotalNeto;
                corte.PedidosConCosto++;
            }
            return corte;
        }

        private static void CopiarTotales(Corte destino, Corte origen)
        {
            destino.NombreUsuario = origen.NombreUsuario;
            destino.TotalPedidos = origen.TotalPedidos;
            destino.PedidosCancelados = origen.PedidosCancelados;
            destino.TotalVentas = origen.TotalVentas;
            destino.MontoCancelado = origen.MontoCancelado;
            destino.TotalDescuentos = origen.TotalDescuentos;
            destino.TotalDevoluciones = origen.TotalDevoluciones;
            destino.TotalNeto = origen.TotalNeto;
            destino.VentasEfectivo = origen.VentasEfectivo;
            destino.VentasTarjeta = origen.VentasTarjeta;
            destino.VentasTransferencia = origen.VentasTransferencia;
            destino.VentasSinMetodo = origen.VentasSinMetodo;
            destino.Costo = origen.Costo;
            destino.VentasConCosto = origen.VentasConCosto;
            destino.PedidosConCosto = origen.PedidosConCosto;
            destino.PedidoIds = origen.PedidoIds;
        }

        /// <summary>De quien es el corte: el propio usuario; un administrador puede elegir a otro.</summary>
        private static Usuario ResolverObjetivo(Usuario actor, int? usuarioId)
        {
            if (!usuarioId.HasValue || usuarioId.Value == actor.Id)
                return actor;

            if (!actor.EsAdministrador)
                throw new ReglaNegocioException("Solo puedes hacer el corte de caja de tus propios días.");

            return UsuarioService.ObtenerPorIdInterno(usuarioId.Value)
                ?? throw new ReglaNegocioException("El usuario ya no existe.");
        }

        private static List<Pedido> PedidosDelDia(List<Pedido> todos, int usuarioId, DateTime fecha) =>
            todos.Where(p => p.UsuarioId == usuarioId && p.FechaHora.Date == fecha.Date).ToList();

        // ---------------------------------------------------------------
        // Dias pendientes y vista previa
        // ---------------------------------------------------------------

        /// <summary>
        /// Dias trabajados sin corte cerrado. Usuario normal: los suyos. Administrador: todos
        /// (o los de un usuario si se indica). Un dia en que no hubo pedidos nunca aparece.
        /// </summary>
        public static List<DiaPorCerrar> ObtenerDiasPendientes(int? usuarioId = null)
        {
            var actor = SesionService.Requerir();
            int? filtro = PedidoService.ResolverFiltroUsuario(actor, usuarioId);

            IEnumerable<Pedido> pedidos = PedidoService.CargarInterno().Where(p => p.UsuarioId != 0);
            if (filtro.HasValue)
                pedidos = pedidos.Where(p => p.UsuarioId == filtro.Value);

            var cerrados = CargarInterno()
                .Where(c => c.EstaCerrado)
                .Select(c => (c.UsuarioId, c.Fecha.Date))
                .ToHashSet();

            var usuarios = UsuarioService.CargarInterno().ToDictionary(u => u.Id, u => u.NombreUsuario);

            var resultado = new List<DiaPorCerrar>();
            foreach (var g in pedidos.GroupBy(p => (p.UsuarioId, Dia: p.FechaHora.Date)))
            {
                if (cerrados.Contains((g.Key.UsuarioId, g.Key.Dia))) continue;

                var lista = g.ToList();
                resultado.Add(new DiaPorCerrar
                {
                    UsuarioId = g.Key.UsuarioId,
                    NombreUsuario = usuarios.TryGetValue(g.Key.UsuarioId, out var nombre) ? nombre : lista[0].UsuarioCreo,
                    Fecha = g.Key.Dia,
                    Pedidos = lista.Count,
                    SinFinalizar = lista.Count(p => !p.EsFinal),
                    TotalNeto = lista.Where(p => !p.EsCancelado).Sum(p => p.TotalNeto)
                });
            }

            return resultado.OrderByDescending(d => d.Fecha).ThenBy(d => d.NombreUsuario).ToList();
        }

        /// <summary>Cantidad de dias ANTERIORES a hoy con pedidos y sin corte cerrado (para el panel del administrador).</summary>
        public static int ContarCortesPendientes()
        {
            SesionService.RequerirAdmin();
            return ObtenerDiasPendientes().Count(d => d.Fecha < DateTime.Today);
        }

        /// <summary>Totales del corte tal como quedarian, sin guardar nada.</summary>
        public static Corte Previsualizar(DateTime fecha, int? usuarioId = null)
        {
            var actor = SesionService.Requerir();
            var objetivo = ResolverObjetivo(actor, usuarioId);

            var delDia = PedidosDelDia(PedidoService.CargarInterno(), objetivo.Id, fecha);
            if (delDia.Count == 0)
                throw new ReglaNegocioException(
                    $"No hay pedidos de {objetivo.NombreUsuario} el {fecha:dd/MM/yyyy}: solo se puede hacer corte de días trabajados.");

            return Calcular(objetivo, fecha, delDia);
        }

        // ---------------------------------------------------------------
        // Cerrar
        // ---------------------------------------------------------------

        public static Corte CerrarCorte(DateTime fecha, string observaciones, int? usuarioId = null)
        {
            var actor = SesionService.Requerir();
            var objetivo = ResolverObjetivo(actor, usuarioId);
            fecha = fecha.Date;

            if (fecha > DateTime.Today)
                throw new ReglaNegocioException("No se puede hacer el corte de una fecha futura.");

            observaciones = (observaciones ?? string.Empty).Trim();
            if (observaciones.Length > LongitudMaximaObservaciones)
                throw new ReglaNegocioException($"Las observaciones no pueden pasar de {LongitudMaximaObservaciones} caracteres.");

            return JsonDataService.Transaccion(() =>
            {
                var cortes = CargarInterno();

                if (DiaCerrado(cortes, objetivo.Id, fecha))
                    throw new ReglaNegocioException(
                        $"El corte de {objetivo.NombreUsuario} del {fecha:dd/MM/yyyy} ya está cerrado.");

                var pedidos = PedidoService.CargarInterno();
                var delDia = PedidosDelDia(pedidos, objetivo.Id, fecha);
                if (delDia.Count == 0)
                    throw new ReglaNegocioException(
                        $"No hay pedidos de {objetivo.NombreUsuario} el {fecha:dd/MM/yyyy}: solo se puede hacer corte de días trabajados.");

                int sinFinalizar = delDia.Count(p => !p.EsFinal);
                if (sinFinalizar > 0)
                    throw new ReglaNegocioException(
                        $"Hay {sinFinalizar} pedido(s) sin finalizar ese día. Márcalos como Entregado o Cancelado antes de hacer el corte.");

                var calculado = Calcular(objetivo, fecha, delDia);

                // Si el corte habia sido reabierto por un administrador, se reutiliza (conserva su auditoria)
                var corte = cortes.FirstOrDefault(c => c.UsuarioId == objetivo.Id && c.Fecha.Date == fecha);
                if (corte == null)
                {
                    corte = calculado;
                    corte.Id = cortes.Count == 0 ? 1 : cortes.Max(c => c.Id) + 1;
                    cortes.Add(corte);
                }
                else
                {
                    CopiarTotales(corte, calculado);
                }

                corte.Estado = Corte.Cerrado;
                corte.FechaCierre = DateTime.Now;
                corte.CerradoPor = actor.NombreUsuario;
                corte.Observaciones = observaciones;

                // Primero se marcan los pedidos y despues se guarda el corte
                foreach (var p in delDia)
                    p.CorteId = corte.Id;
                PedidoService.GuardarInterno(pedidos);
                JsonDataService.Guardar(Archivo, cortes);

                return corte;
            });
        }

        // ---------------------------------------------------------------
        // Consultar
        // ---------------------------------------------------------------

        /// <summary>Administrador: todos (con filtros). Usuario normal: solo los suyos.</summary>
        public static List<Corte> ObtenerCortes(DateTime? desde = null, DateTime? hasta = null, int? usuarioId = null)
        {
            var actor = SesionService.Requerir();
            int? filtro = PedidoService.ResolverFiltroUsuario(actor, usuarioId);

            IEnumerable<Corte> q = CargarInterno();
            if (filtro.HasValue) q = q.Where(c => c.UsuarioId == filtro.Value);
            if (desde.HasValue) q = q.Where(c => c.Fecha.Date >= desde.Value.Date);
            if (hasta.HasValue) q = q.Where(c => c.Fecha.Date <= hasta.Value.Date);

            return q.OrderByDescending(c => c.Fecha).ThenBy(c => c.NombreUsuario).ToList();
        }

        public static Corte ObtenerCorte(int id) =>
            ObtenerCortes().FirstOrDefault(c => c.Id == id);

        /// <summary>Pedidos incluidos en un corte (con permiso: el dueño del corte o un administrador).</summary>
        public static List<Pedido> ObtenerPedidosDelCorte(int corteId)
        {
            var corte = ObtenerCorte(corteId)
                ?? throw new ReglaNegocioException("No tienes acceso a ese corte o ya no existe.");

            var ids = new HashSet<int>(corte.PedidoIds);
            return PedidoService.Consultar(null, corte.Fecha, corte.Fecha, corte.UsuarioId)
                .Where(p => ids.Contains(p.Id))
                .OrderBy(p => p.FechaHora)
                .ToList();
        }

        // ---------------------------------------------------------------
        // Texto del corte (para mostrar en pantalla)
        // ---------------------------------------------------------------
        public static string GenerarTexto(Corte corte, IEnumerable<Pedido> pedidos = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("           CORTE DE CAJA - SIBERIA");
            sb.AppendLine("========================================");
            sb.AppendLine($"Usuario:   {corte.NombreUsuario}");
            sb.AppendLine($"Día:       {corte.Fecha:dd/MM/yyyy}");
            sb.AppendLine(corte.EstaCerrado
                ? $"Estado:    CERRADO ({corte.FechaCierre:dd/MM/yyyy HH:mm} por {corte.CerradoPor})"
                : corte.Id == 0 ? "Estado:    Sin cerrar (vista previa)" : "Estado:    ABIERTO (reabierto)");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine($"Pedidos válidos:      {corte.TotalPedidos,10}");
            sb.AppendLine($"Pedidos cancelados:   {corte.PedidosCancelados,10}");
            sb.AppendLine($"Ventas registradas:   {Formato.Dinero(corte.TotalVentas),10}");
            sb.AppendLine($"(-) Cancelaciones:    {Formato.Dinero(corte.MontoCancelado),10}");
            sb.AppendLine($"(-) Devoluciones:     {Formato.Dinero(corte.TotalDevoluciones),10}");
            sb.AppendLine($"TOTAL NETO (ingreso): {Formato.Dinero(corte.TotalNeto),10}");
            sb.AppendLine($"Descuentos dados:     {Formato.Dinero(corte.TotalDescuentos),10}  (ya restados de las ventas)");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("Ingreso neto por forma de pago:");
            sb.AppendLine($"  Efectivo:           {Formato.Dinero(corte.VentasEfectivo),10}");
            sb.AppendLine($"  Tarjeta:            {Formato.Dinero(corte.VentasTarjeta),10}");
            sb.AppendLine($"  Transferencia:      {Formato.Dinero(corte.VentasTransferencia),10}");
            if (corte.VentasSinMetodo != 0)
                sb.AppendLine($"  Sin registrar:      {Formato.Dinero(corte.VentasSinMetodo),10}  (pedidos anteriores)");
            sb.AppendLine("----------------------------------------");
            if (corte.HayUtilidad)
            {
                sb.AppendLine($"Costo ({corte.PedidosConCosto} de {corte.TotalPedidos} pedidos con costo): {Formato.Dinero(corte.Costo)}");
                sb.AppendLine($"Utilidad sobre esos pedidos: {Formato.Dinero(corte.Utilidad)}");
            }
            else
            {
                sb.AppendLine("Utilidad: no calculable (faltan costos en el menú).");
            }

            if (!string.IsNullOrWhiteSpace(corte.Observaciones))
            {
                sb.AppendLine("----------------------------------------");
                sb.AppendLine("Observaciones: " + corte.Observaciones);
            }
            if (!string.IsNullOrWhiteSpace(corte.ReabiertoPor))
            {
                sb.AppendLine("----------------------------------------");
                sb.AppendLine($"Reabierto por {corte.ReabiertoPor} el {corte.FechaReapertura:dd/MM/yyyy HH:mm}");
                sb.AppendLine("Motivo: " + corte.MotivoReapertura);
            }

            if (pedidos != null)
            {
                sb.AppendLine("----------------------------------------");
                sb.AppendLine("Pedidos incluidos:");
                foreach (var p in pedidos.OrderBy(x => x.FechaHora))
                {
                    sb.AppendLine($"  {p.Folio}  {p.Cliente}  {Formato.Dinero(p.Total)}  [{p.Estado}] {p.MetodoPagoTexto}");
                    foreach (var d in p.Detalles)
                    {
                        sb.AppendLine($"     {d.Cantidad} x {d.NombreProducto}");
                        foreach (var n in d.Notas ?? new List<NotaProducto>())
                            sb.AppendLine($"        - {n.Cantidad} {n.Texto}");
                    }
                }
            }
            sb.AppendLine("========================================");
            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // Reabrir (procedimiento autorizado: solo administrador, con motivo)
        // ---------------------------------------------------------------
        public static Corte ReabrirCorte(int corteId, string motivo)
        {
            var actor = SesionService.RequerirAdmin();

            motivo = (motivo ?? string.Empty).Trim();
            if (motivo.Length < LongitudMinimaMotivo)
                throw new ReglaNegocioException($"Escribe el motivo de la reapertura (mínimo {LongitudMinimaMotivo} caracteres).");
            if (motivo.Length > LongitudMaximaObservaciones)
                throw new ReglaNegocioException($"El motivo no puede pasar de {LongitudMaximaObservaciones} caracteres.");

            return JsonDataService.Transaccion(() =>
            {
                var cortes = CargarInterno();
                var corte = cortes.FirstOrDefault(c => c.Id == corteId)
                    ?? throw new ReglaNegocioException("El corte ya no existe.");

                if (!corte.EstaCerrado)
                    throw new ReglaNegocioException("Ese corte ya está abierto.");

                corte.Estado = Corte.Abierto;
                corte.ReabiertoPor = actor.NombreUsuario;
                corte.FechaReapertura = DateTime.Now;
                corte.MotivoReapertura = motivo;
                JsonDataService.Guardar(Archivo, cortes);
                return corte;
            });
        }
    }
}
