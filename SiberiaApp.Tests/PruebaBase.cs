using SiberiaApp.Models;
using SiberiaApp.Services;
using Xunit;

// Las pruebas comparten estado estatico (sesion y carpeta de datos): se ejecutan una por una
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SiberiaApp.Tests;

/// <summary>Cada prueba arranca con una carpeta de datos temporal vacia y el administrador inicial (admin / admin123).</summary>
public abstract class PruebaBase : IDisposable
{
    protected const string Clave = "clave123";
    private readonly string _carpeta;

    protected PruebaBase()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "siberia-test-" + Guid.NewGuid().ToString("N"));
        JsonDataService.CarpetaDatos = _carpeta;
        SesionService.Cerrar();
        UsuarioService.ReiniciarBloqueos();
        MigracionDatos.Ejecutar();
    }

    public void Dispose()
    {
        SesionService.Cerrar();
        try { Directory.Delete(_carpeta, true); } catch { /* no importa si no se puede borrar */ }
    }

    protected string Datos => JsonDataService.CarpetaDatos;

    protected Usuario EntrarComoAdmin()
    {
        var u = UsuarioService.Autenticar("admin", "admin123");
        Assert.NotNull(u);
        SesionService.Iniciar(u);
        return u;
    }

    /// <summary>Crea un cajero (como administrador) y deja la sesion cerrada.</summary>
    protected Usuario CrearCajero(string nombre)
    {
        EntrarComoAdmin();
        var u = UsuarioService.Crear(nombre, Clave, Roles.Cajero);
        SesionService.Cerrar();
        return u;
    }

    protected void EntrarComo(string nombre)
    {
        var u = UsuarioService.Autenticar(nombre, Clave);
        Assert.NotNull(u);
        SesionService.Iniciar(u);
    }

    protected MenuItem Producto(int id) => MenuService.ObtenerTodos().First(m => m.Id == id);

    protected DetallePedido Linea(int productoId, int cantidad, params (int cantidad, string texto)[] notas)
    {
        var p = Producto(productoId);
        return new DetallePedido
        {
            MenuItemId = p.Id,
            NombreProducto = p.Nombre,
            PrecioUnitario = p.Precio,
            Cantidad = cantidad,
            Notas = notas.Select(n => new NotaProducto { Cantidad = n.cantidad, Texto = n.texto }).ToList()
        };
    }

    protected Pedido Pedir(string cliente, params DetallePedido[] lineas) =>
        PedidoService.Guardar(new Pedido { MetodoPago = "Efectivo", Cliente = cliente, TipoPedido = "Mostrador", Detalles = lineas.ToList() });

    protected Pedido PedirYEntregar(string cliente, params DetallePedido[] lineas)
    {
        var p = Pedir(cliente, lineas);
        return PedidoService.CambiarEstado(p.Id, EstadosPedido.Entregado);
    }

    /// <summary>Pasa un pedido al dia de ayer (para probar cortes de dias anteriores).</summary>
    protected void MoverPedidoAAyer(int pedidoId)
    {
        var lista = PedidoService.CargarInterno();
        lista.First(p => p.Id == pedidoId).FechaHora = DateTime.Now.AddDays(-1);
        PedidoService.GuardarInterno(lista);
    }

    protected static ReglaNegocioException Falla(Action accion, string mensajeContiene = null)
    {
        var ex = Assert.ThrowsAny<ReglaNegocioException>(accion);
        if (mensajeContiene != null)
            Assert.Contains(mensajeContiene, ex.Message, StringComparison.OrdinalIgnoreCase);
        return ex;
    }
}
