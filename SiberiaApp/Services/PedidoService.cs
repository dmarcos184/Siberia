using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SiberiaApp.Helpers;
using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Pedidos: crear, consultar y cambiar de estado.
    /// Todas las reglas (permisos, validaciones, calculo del total, duplicados, cortes cerrados)
    /// viven aqui, no en las pantallas. La pantalla solo muestra los mensajes.
    /// </summary>
    public static class PedidoService
    {
        private const string Archivo = "pedidos.json";

        public const int CantidadMaximaPorProducto = 10;
        public const int MaximoNotasPorProducto = 10;
        public const int LongitudMaximaNota = 60;
        public const int LongitudMaximaCliente = 60;
        public const int LongitudMaximaDireccion = 120;
        public const int LongitudMaximaMotivo = 200;
        private const int SegundosAntiDuplicado = 15;

        /// <summary>
        /// Descuento maximo (en % del importe del pedido) que puede dar un cajero. Un administrador puede
        /// llegar hasta el importe completo. Cambia este numero si el negocio quiere otro limite.
        /// </summary>
        public const decimal DescuentoMaximoCajeroPorcentaje = 10m;

        // ---------------------------------------------------------------
        // Acceso interno (sin filtrar por usuario). Las pantallas NO lo usan.
        // ---------------------------------------------------------------
        internal static List<Pedido> CargarInterno()
        {
            var lista = JsonDataService.Cargar<Pedido>(Archivo);
            foreach (var p in lista)
            {
                p.Detalles ??= new List<DetallePedido>();
                foreach (var d in p.Detalles)
                    d.Notas ??= new List<NotaProducto>();
            }
            return lista;
        }

        internal static void GuardarInterno(List<Pedido> lista) => JsonDataService.Guardar(Archivo, lista);

        internal static bool UsuarioTieneHistorial(int usuarioId) =>
            CargarInterno().Any(p => p.UsuarioId == usuarioId);

        internal static bool ProductoTieneVentas(int menuItemId) =>
            CargarInterno().Any(p => p.Detalles.Any(d => d.MenuItemId == menuItemId));

        // ---------------------------------------------------------------
        // Consultas (filtradas segun quien este conectado)
        // ---------------------------------------------------------------

        /// <summary>
        /// Administrador: puede ver todo y filtrar por usuario.
        /// Usuario normal: SIEMPRE solo lo suyo; si pide los de otro usuario se rechaza.
        /// </summary>
        internal static int? ResolverFiltroUsuario(Usuario actor, int? usuarioId)
        {
            if (actor.EsAdministrador) return usuarioId;

            if (usuarioId.HasValue && usuarioId.Value != actor.Id)
                throw new ReglaNegocioException("Solo puedes consultar tu propia información.");
            return actor.Id;
        }

        /// <summary>Pedidos que el usuario actual tiene permitido ver.</summary>
        public static List<Pedido> ObtenerTodos() => Consultar();

        public static Pedido ObtenerPorId(int id) =>
            Consultar().FirstOrDefault(p => p.Id == id);

        public static List<Pedido> Consultar(string texto = null, DateTime? desde = null,
                                             DateTime? hasta = null, int? usuarioId = null)
        {
            var actor = SesionService.Requerir();
            int? filtroUsuario = ResolverFiltroUsuario(actor, usuarioId);

            IEnumerable<Pedido> q = CargarInterno();
            if (filtroUsuario.HasValue)
                q = q.Where(p => p.UsuarioId == filtroUsuario.Value);
            if (desde.HasValue)
                q = q.Where(p => p.FechaHora.Date >= desde.Value.Date);
            if (hasta.HasValue)
                q = q.Where(p => p.FechaHora.Date <= hasta.Value.Date);

            if (!string.IsNullOrWhiteSpace(texto))
            {
                string t = texto.Trim();
                q = q.Where(p =>
                    (p.Cliente ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    (p.Folio ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase));
            }

            return q.OrderByDescending(p => p.FechaHora).ToList();
        }

        public static List<Pedido> BuscarPorClienteOFolio(string texto) => Consultar(texto);

        public static List<Pedido> ReporteDiario(DateTime fecha) =>
            Consultar(null, fecha, fecha)
                .Where(p => !p.EsCancelado)
                .OrderBy(p => p.FechaHora)
                .ToList();

        public static List<Pedido> ReporteSemanal(DateTime fecha)
        {
            var (desde, hasta) = Periodos.Rango(TipoPeriodo.Semana, fecha);
            return Consultar(null, desde, hasta)
                .Where(p => !p.EsCancelado)
                .OrderBy(p => p.FechaHora)
                .ToList();
        }

        // ---------------------------------------------------------------
        // Crear pedido
        // ---------------------------------------------------------------

        /// <summary>
        /// Valida y guarda un pedido nuevo. El servidor (esta clase) recalcula todo:
        /// precios y costos salen del menu, el total sale de los renglones, y quien lo capturo
        /// sale de la sesion. Lo que mande la pantalla solo se usa para comparar.
        /// </summary>
        /// <param name="totalMostrado">Total que vio el usuario en pantalla (si no coincide, se rechaza).</param>
        public static Pedido Guardar(Pedido pedido, decimal? totalMostrado = null)
        {
            var actor = SesionService.Requerir();
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // ---- Datos generales ----
            string cliente = Regex.Replace((pedido.Cliente ?? string.Empty).Trim(), @"\s+", " ");
            if (cliente.Length == 0)
                throw new ReglaNegocioException("Escribe el nombre del cliente.");
            if (cliente.Length > LongitudMaximaCliente)
                throw new ReglaNegocioException($"El nombre del cliente no puede pasar de {LongitudMaximaCliente} caracteres.");

            string tipo = pedido.TipoPedido ?? string.Empty;
            if (!TiposPedido.Todos.Contains(tipo))
                throw new ReglaNegocioException("Elige un tipo de pedido válido.");

            string direccion = string.Empty;
            string telefono = string.Empty;
            if (tipo == TiposPedido.Domicilio)
            {
                direccion = Regex.Replace((pedido.Direccion ?? string.Empty).Trim(), @"\s+", " ");
                telefono = (pedido.Telefono ?? string.Empty).Trim();

                if (direccion.Length == 0 || telefono.Length == 0)
                    throw new ReglaNegocioException("Para pedidos a domicilio captura dirección y teléfono.");
                if (direccion.Length > LongitudMaximaDireccion)
                    throw new ReglaNegocioException($"La dirección no puede pasar de {LongitudMaximaDireccion} caracteres.");
                if (!Regex.IsMatch(Regex.Replace(telefono, @"[\s\-\(\)]", string.Empty), @"^\+?\d{7,15}$"))
                    throw new ReglaNegocioException("El teléfono no es válido (solo números, de 7 a 15 dígitos).");
            }

            if (pedido.Detalles == null || pedido.Detalles.Count == 0)
                throw new ReglaNegocioException("Agrega al menos un producto al pedido.");

            string metodoPago = pedido.MetodoPago ?? string.Empty;
            if (!MetodosPago.Todos.Contains(metodoPago))
                throw new ReglaNegocioException("Elige el método de pago (efectivo, tarjeta o transferencia).");

            return JsonDataService.Transaccion(() =>
            {
                // ---- Renglones: se vuelven a validar contra el menu actual ----
                var detalles = NormalizarDetalles(pedido.Detalles, MenuService.CargarInterno());

                decimal subtotal = detalles.Sum(d => d.Subtotal);
                decimal descuento = ValidarDescuento(pedido.Descuento, subtotal, actor);
                decimal total = subtotal - descuento;
                if (totalMostrado.HasValue && totalMostrado.Value != total)
                    throw new ReglaNegocioException(
                        $"El total mostrado ({Formato.Dinero(totalMostrado.Value)}) no coincide con el calculado " +
                        $"({Formato.Dinero(total)}). Revisa el pedido e inténtalo de nuevo.");

                // ---- No se vende en un dia ya cerrado ----
                var ahora = DateTime.Now;
                if (CorteService.DiaCerrado(CorteService.CargarInterno(), actor.Id, ahora))
                    throw new ReglaNegocioException(
                        $"Ya cerraste tu corte de caja de hoy ({ahora:dd/MM/yyyy}), por eso no se pueden registrar más pedidos. " +
                        "Un administrador puede reabrir el corte si hace falta.");

                var lista = CargarInterno();

                var nuevo = new Pedido
                {
                    Cliente = cliente,
                    TipoPedido = tipo,
                    Direccion = direccion,
                    Telefono = telefono,
                    FechaHora = ahora,
                    Detalles = detalles,
                    Estado = EstadosPedido.Pendiente,
                    MetodoPago = metodoPago,
                    Descuento = descuento,
                    UsuarioCreo = actor.NombreUsuario,
                    UsuarioId = actor.Id,
                    CorteId = 0
                };

                // ---- Evitar duplicados por doble clic / doble envio ----
                string firma = Firma(nuevo);
                var repetido = lista.FirstOrDefault(p =>
                    p.UsuarioId == actor.Id &&
                    p.FechaHora >= ahora.AddSeconds(-SegundosAntiDuplicado) &&
                    p.Cliente.Equals(cliente, StringComparison.OrdinalIgnoreCase) &&
                    Firma(p) == firma);
                if (repetido != null)
                    throw new ReglaNegocioException(
                        $"Ya se registró un pedido idéntico hace unos segundos (folio {repetido.Folio}). " +
                        "Si de verdad es otro pedido, espera un momento y vuelve a guardarlo.");

                nuevo.Id = lista.Count == 0 ? 1 : lista.Max(p => p.Id) + 1;
                nuevo.Folio = $"SIB-{nuevo.FechaHora:yyyyMMdd}-{nuevo.Id:D4}";

                lista.Add(nuevo);
                GuardarInterno(lista);
                return nuevo;
            });
        }

        /// <summary>
        /// Reglas del descuento: no negativo, solo centavos, no mayor al importe del pedido y,
        /// para un cajero, no mayor al porcentaje permitido.
        /// </summary>
        internal static decimal ValidarDescuento(decimal descuento, decimal subtotal, Usuario actor)
        {
            if (descuento < 0)
                throw new ReglaNegocioException("El descuento no puede ser negativo.");
            if (descuento != Math.Round(descuento, 2))
                throw new ReglaNegocioException("El descuento solo puede tener centavos (2 decimales).");
            if (descuento > subtotal)
                throw new ReglaNegocioException(
                    $"El descuento ({Formato.Dinero(descuento)}) no puede ser mayor al importe del pedido ({Formato.Dinero(subtotal)}).");

            if (!actor.EsAdministrador)
            {
                decimal maximo = Math.Round(subtotal * DescuentoMaximoCajeroPorcentaje / 100m, 2);
                if (descuento > maximo)
                    throw new ReglaNegocioException(
                        $"Como cajero puedes dar hasta {DescuentoMaximoCajeroPorcentaje:0.##}% de descuento " +
                        $"({Formato.Dinero(maximo)} en este pedido). Pide a un administrador un descuento mayor.");
            }
            return descuento;
        }

        private static string Firma(Pedido p) =>
            string.Join("|", p.Detalles
                .OrderBy(d => d.MenuItemId)
                .Select(d => $"{d.MenuItemId}x{d.Cantidad}:" + string.Join(",",
                    (d.Notas ?? new List<NotaProducto>())
                        .Select(n => n.Cantidad + (n.Texto ?? string.Empty).ToLowerInvariant())
                        .OrderBy(s => s))));

        /// <summary>
        /// Revisa cada renglon contra el menu y devuelve renglones limpios.
        /// Precio y costo se toman del menu (no se confia en lo que mande la pantalla).
        /// </summary>
        internal static List<DetallePedido> NormalizarDetalles(IEnumerable<DetallePedido> origen, List<MenuItem> menu)
        {
            var resultado = new List<DetallePedido>();

            foreach (var d in origen)
            {
                if (d == null)
                    throw new ReglaNegocioException("El pedido tiene un renglón inválido.");

                var item = menu.FirstOrDefault(m => m.Id == d.MenuItemId);
                if (item == null)
                    throw new ReglaNegocioException($"El producto \"{d.NombreProducto}\" ya no existe en el menú.");
                if (!item.Activo)
                    throw new ReglaNegocioException($"El producto \"{item.Nombre}\" está desactivado y no se puede vender.");

                if (d.Cantidad <= 0)
                    throw new ReglaNegocioException($"La cantidad de \"{item.Nombre}\" debe ser mayor a cero.");
                if (d.Cantidad > CantidadMaximaPorProducto)
                    throw new ReglaNegocioException(
                        $"La cantidad por producto debe ser entre 1 y {CantidadMaximaPorProducto} (\"{item.Nombre}\").");

                if (d.PrecioUnitario < 0)
                    throw new ReglaNegocioException($"El precio de \"{item.Nombre}\" no puede ser negativo.");
                if (d.PrecioUnitario != item.Precio)
                    throw new PrecioCambiadoException(
                        $"El precio de \"{item.Nombre}\" cambió de {Formato.Dinero(d.PrecioUnitario)} a " +
                        $"{Formato.Dinero(item.Precio)}. Se actualizó en el pedido: revísalo y guárdalo de nuevo.");

                resultado.Add(new DetallePedido
                {
                    MenuItemId = item.Id,
                    NombreProducto = item.Nombre,
                    PrecioUnitario = item.Precio,
                    Cantidad = d.Cantidad,
                    CostoUnitario = item.Costo,
                    Notas = ValidarNotas(d, item.Nombre)
                });
            }

            var excedido = resultado.GroupBy(r => r.MenuItemId).FirstOrDefault(g => g.Sum(r => r.Cantidad) > CantidadMaximaPorProducto);
            if (excedido != null)
                throw new ReglaNegocioException(
                    $"Solo se pueden agregar hasta {CantidadMaximaPorProducto} unidades de \"{excedido.First().NombreProducto}\".");

            return resultado;
        }

        /// <summary>Valida y limpia las notas de un renglon. Cada nota aplica a 1..Cantidad unidades.</summary>
        public static List<NotaProducto> ValidarNotas(DetallePedido detalle, string nombreProducto)
        {
            var notas = detalle.Notas ?? new List<NotaProducto>();
            if (notas.Count > MaximoNotasPorProducto)
                throw new ReglaNegocioException(
                    $"\"{nombreProducto}\" no puede tener más de {MaximoNotasPorProducto} notas.");

            var limpias = new List<NotaProducto>();
            foreach (var n in notas)
            {
                string texto = Regex.Replace((n?.Texto ?? string.Empty).Trim(), @"\s+", " ");
                if (texto.Length == 0)
                    throw new ReglaNegocioException($"Hay una nota vacía en \"{nombreProducto}\".");
                if (texto.Length > LongitudMaximaNota)
                    throw new ReglaNegocioException(
                        $"Las notas no pueden pasar de {LongitudMaximaNota} caracteres (\"{nombreProducto}\").");
                if (n.Cantidad < 1 || n.Cantidad > detalle.Cantidad)
                    throw new ReglaNegocioException(
                        $"La nota \"{texto}\" aplica a {n.Cantidad} unidad(es), pero \"{nombreProducto}\" lleva {detalle.Cantidad}.");

                limpias.Add(new NotaProducto { Cantidad = n.Cantidad, Texto = texto });
            }
            return limpias;
        }

        // ---------------------------------------------------------------
        // Cambiar estado
        // ---------------------------------------------------------------

        /// <summary>
        /// Cambia el estado de un pedido.
        ///  - Usuario normal: solo sus pedidos y solo mientras no esten finalizados (Entregado/Cancelado).
        ///  - Administrador: cualquiera, incluso finalizados (esa es la "autorizacion").
        ///  - Nadie: si el dia de ese pedido ya tiene el corte cerrado (hay que reabrir el corte).
        /// </summary>
        public static Pedido CambiarEstado(int pedidoId, string nuevoEstado)
        {
            var actor = SesionService.Requerir();

            if (!EstadosPedido.Todos.Contains(nuevoEstado))
                throw new ReglaNegocioException("Selecciona un estado válido.");

            return JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                var pedido = lista.FirstOrDefault(p => p.Id == pedidoId)
                    ?? throw new ReglaNegocioException("El pedido ya no existe.");

                if (!actor.EsAdministrador && pedido.UsuarioId != actor.Id)
                    throw new ReglaNegocioException("No tienes permiso para modificar pedidos de otro usuario.");

                if (pedido.Estado == nuevoEstado)
                    throw new ReglaNegocioException($"El pedido ya se encuentra en estado \"{nuevoEstado}\".");

                if (CorteService.EstaBloqueado(pedido, CorteService.CargarInterno()))
                    throw new ReglaNegocioException(
                        $"El pedido {pedido.Folio} pertenece a un corte de caja cerrado y no se puede modificar. " +
                        "Un administrador debe reabrir el corte primero.");

                if (pedido.EsFinal && !actor.EsAdministrador)
                    throw new ReglaNegocioException(
                        $"El pedido {pedido.Folio} ya está \"{pedido.Estado}\". Solo un administrador puede modificarlo.");

                if (pedido.MontoDevuelto > 0 && nuevoEstado != EstadosPedido.Entregado)
                    throw new ReglaNegocioException(
                        $"El pedido {pedido.Folio} ya tiene una devolución registrada ({Formato.Dinero(pedido.MontoDevuelto)}) " +
                        "y no puede cambiar de estado.");

                pedido.Estado = nuevoEstado;
                GuardarInterno(lista);
                return pedido;
            });
        }

        /// <summary>
        /// Registra dinero devuelto al cliente de un pedido ENTREGADO (solo administrador).
        /// Se pueden hacer varias devoluciones parciales mientras no se pase del total cobrado.
        /// No se permite si el dia del pedido ya tiene el corte cerrado.
        /// </summary>
        public static Pedido RegistrarDevolucion(int pedidoId, decimal monto, string motivo)
        {
            var actor = SesionService.RequerirAdmin();

            motivo = Regex.Replace((motivo ?? string.Empty).Trim(), @"\s+", " ");
            if (motivo.Length < 5)
                throw new ReglaNegocioException("Escribe el motivo de la devolución (mínimo 5 caracteres).");
            if (motivo.Length > LongitudMaximaMotivo)
                throw new ReglaNegocioException($"El motivo no puede pasar de {LongitudMaximaMotivo} caracteres.");
            if (monto <= 0)
                throw new ReglaNegocioException("El monto de la devolución debe ser mayor a cero.");
            if (monto != Math.Round(monto, 2))
                throw new ReglaNegocioException("El monto solo puede tener centavos (2 decimales).");

            return JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                var pedido = lista.FirstOrDefault(p => p.Id == pedidoId)
                    ?? throw new ReglaNegocioException("El pedido ya no existe.");

                if (pedido.Estado != EstadosPedido.Entregado)
                    throw new ReglaNegocioException("Solo se pueden registrar devoluciones de pedidos entregados.");

                if (CorteService.EstaBloqueado(pedido, CorteService.CargarInterno()))
                    throw new ReglaNegocioException(
                        $"El pedido {pedido.Folio} pertenece a un corte de caja cerrado. Reabre el corte primero.");

                decimal disponible = pedido.Total - pedido.MontoDevuelto;
                if (monto > disponible)
                    throw new ReglaNegocioException(
                        $"La devolución no puede pasar de {Formato.Dinero(disponible)} " +
                        "(lo que queda por devolver de este pedido).");

                pedido.MontoDevuelto += monto;
                pedido.MotivoDevolucion = string.IsNullOrEmpty(pedido.MotivoDevolucion)
                    ? motivo
                    : pedido.MotivoDevolucion + " | " + motivo;
                pedido.DevueltoPor = actor.NombreUsuario;
                pedido.FechaDevolucion = DateTime.Now;

                GuardarInterno(lista);
                return pedido;
            });
        }

        /// <summary>Explica por que un pedido no se puede editar (null = si se puede). Para la pantalla.</summary>
        public static string MotivoNoEditable(Pedido pedido)
        {
            var actor = SesionService.Requerir();
            if (!actor.EsAdministrador && pedido.UsuarioId != actor.Id)
                return "Este pedido es de otro usuario.";
            if (CorteService.EstaBloqueado(pedido, CorteService.CargarInterno()))
                return "Pertenece a un corte cerrado. Un administrador debe reabrir el corte.";
            if (pedido.EsFinal && !actor.EsAdministrador)
                return $"Ya está \"{pedido.Estado}\". Solo un administrador puede modificarlo.";
            return null;
        }

        // ---------------------------------------------------------------
        // Ticket
        // ---------------------------------------------------------------
        public static string GenerarTicket(Pedido pedido)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("            SIBERIA - TICKET");
            sb.AppendLine("========================================");
            sb.AppendLine($"Folio:      {pedido.Folio}");
            sb.AppendLine($"Fecha:      {pedido.FechaHora:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Cliente:    {pedido.Cliente}");
            sb.AppendLine($"Tipo:       {pedido.TipoPedido}");
            if (pedido.TipoPedido == "Domicilio")
            {
                sb.AppendLine($"Direccion:  {pedido.Direccion}");
                sb.AppendLine($"Telefono:   {pedido.Telefono}");
            }
            sb.AppendLine("----------------------------------------");
            foreach (var d in pedido.Detalles)
            {
                sb.AppendLine($"{d.Cantidad,2} x {d.NombreProducto,-20} ${d.Subtotal,7:F2}");
                if (d.Notas != null)
                    foreach (var n in d.Notas)
                        sb.AppendLine($"      - {n.Cantidad} {n.Texto}");
            }
            sb.AppendLine("----------------------------------------");
            if (pedido.Descuento > 0)
            {
                sb.AppendLine($"SUBTOTAL:                     ${pedido.Subtotal,7:F2}");
                sb.AppendLine($"DESCUENTO:                   -${pedido.Descuento,7:F2}");
            }
            sb.AppendLine($"TOTAL:                        ${pedido.Total,7:F2}");
            sb.AppendLine($"Pago: {pedido.MetodoPagoTexto}");
            if (pedido.MontoDevuelto > 0)
            {
                sb.AppendLine($"DEVUELTO:                    -${pedido.MontoDevuelto,7:F2}");
                sb.AppendLine($"NETO:                         ${pedido.TotalNeto,7:F2}");
                sb.AppendLine($"Motivo: {pedido.MotivoDevolucion}");
            }
            sb.AppendLine("========================================");
            sb.AppendLine("       Atendio: " + pedido.UsuarioCreo);
            sb.AppendLine("      ¡Gracias por su compra!");
            sb.AppendLine("========================================");
            return sb.ToString();
        }
    }
}
