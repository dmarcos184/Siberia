using SiberiaApp.Models;
using SiberiaApp.Services;
using Xunit;

namespace SiberiaApp.Tests;

public class ReportesTests : PruebaBase
{
    private readonly Usuario _pedro;
    private readonly Usuario _ana;

    public ReportesTests()
    {
        _pedro = CrearCajero("Pedro");
        _ana = CrearCajero("Ana");
    }

    private void VentasDeEjemplo()
    {
        EntrarComo("Pedro");
        PedirYEntregar("Luis", Linea(1, 2));   // 170
        EntrarComo("Ana");
        PedirYEntregar("Maria", Linea(6, 1));  // 65
        var c = Pedir("Sofia", Linea(2, 1));   // 75 cancelado: no cuenta como venta
        PedidoService.CambiarEstado(c.Id, EstadosPedido.Cancelado);
    }

    [Fact]
    public void El_administrador_ve_las_ventas_de_todo_el_negocio_y_el_usuario_solo_las_suyas()
    {
        VentasDeEjemplo();

        EntrarComoAdmin();
        var todo = ReporteService.ObtenerResumen(DateTime.Today, DateTime.Today);
        Assert.Equal(235m, todo.Ventas);
        Assert.Equal(2, todo.Pedidos);
        Assert.Equal(1, todo.Cancelados);
        Assert.Equal(75m, todo.MontoCancelado);
        Assert.Equal(117.50m, todo.TicketPromedio);

        Assert.Equal(170m, ReporteService.ObtenerResumen(DateTime.Today, DateTime.Today, _pedro.Id).Ventas);

        EntrarComo("Ana");
        Assert.Equal(65m, ReporteService.ObtenerResumen(DateTime.Today, DateTime.Today).Ventas);
        Falla(() => ReporteService.ObtenerResumen(DateTime.Today, DateTime.Today, _pedro.Id), "propia información");
        Falla(() => ReporteService.ObtenerPanelAdmin());
    }

    [Fact]
    public void Sin_costos_capturados_la_ganancia_no_se_calcula_ni_se_inventa()
    {
        VentasDeEjemplo();
        EntrarComoAdmin();

        var r = ReporteService.ObtenerResumen(DateTime.Today, DateTime.Today);
        Assert.False(r.HayUtilidad);
        Assert.Equal(2, r.PedidosSinCosto);
        Assert.Contains("No calculable", r.NotaUtilidad);
    }

    [Fact]
    public void Con_costos_la_ganancia_es_ventas_menos_costo_y_distingue_lo_que_falta()
    {
        EntrarComoAdmin();
        var tostada = Producto(1);          // 85
        tostada.Costo = 30m;
        MenuService.Actualizar(tostada);    // el Taco (id 6) queda SIN costo

        EntrarComo("Pedro");
        PedirYEntregar("Luis", Linea(1, 2));   // venta 170, costo 60 -> utilidad 110
        PedirYEntregar("Maria", Linea(6, 1));  // venta 65, sin costo

        // cambiar el costo despues no altera las ventas ya hechas
        EntrarComoAdmin();
        tostada = Producto(1);
        tostada.Costo = 80m;
        MenuService.Actualizar(tostada);

        var r = ReporteService.ObtenerResumen(DateTime.Today, DateTime.Today);
        Assert.Equal(235m, r.Ventas);
        Assert.True(r.HayUtilidad);
        Assert.Equal(1, r.PedidosConCosto);
        Assert.Equal(60m, r.Costo);
        Assert.Equal(110m, r.Utilidad);          // solo sobre el pedido con costo
        Assert.False(r.UtilidadCompleta);
        Assert.Contains("Parcial", r.NotaUtilidad);
    }

    [Fact]
    public void Productos_mas_vendidos_y_comparacion_con_el_periodo_anterior()
    {
        EntrarComo("Pedro");
        var ayer = PedirYEntregar("Luis", Linea(1, 2));        // 170 ayer
        MoverPedidoAAyer(ayer.Id);
        PedirYEntregar("Maria", Linea(1, 3), Linea(6, 1));     // 255 + 65 = 320 hoy

        EntrarComoAdmin();
        var cmp = ReporteService.Comparar(DateTime.Today, DateTime.Today);
        Assert.Equal(320m, cmp.Actual.Ventas);
        Assert.Equal(170m, cmp.Anterior.Ventas);
        Assert.Equal(88.2m, cmp.VariacionVentas);

        Assert.Equal("Tostada Especial", cmp.Actual.TopProductos[0].Nombre);
        Assert.Equal(3, cmp.Actual.TopProductos[0].Cantidad);

        // sin ventas en el periodo anterior no hay variacion que inventar
        var sinPrevio = ReporteService.Comparar(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(-30));
        Assert.Null(sinPrevio.VariacionVentas);
    }

    [Fact]
    public void El_panel_del_administrador_usa_datos_reales()
    {
        VentasDeEjemplo();
        EntrarComoAdmin();

        var panel = ReporteService.ObtenerPanelAdmin();
        Assert.Equal(235m, panel.Hoy.Actual.Ventas);
        Assert.Equal(3, panel.UsuariosActivos);       // admin + Pedro + Ana
        Assert.Equal(0, panel.PedidosEnCurso);
        Assert.Equal(7, panel.UltimosSieteDias.Count);
        Assert.Equal(235m, panel.UltimosSieteDias.Last().Ventas);
        Assert.Equal(0m, panel.UltimosSieteDias.First().Ventas);
    }

    [Fact]
    public void Los_periodos_calculan_bien_semana_mes_y_rango_personalizado()
    {
        var miercoles = new DateTime(2026, 11, 4);
        Assert.Equal((new DateTime(2026, 11, 1), new DateTime(2026, 11, 7)), Periodos.Rango(TipoPeriodo.Semana, miercoles));
        Assert.Equal((new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)), Periodos.Rango(TipoPeriodo.Mes, miercoles));
        Assert.Equal((miercoles, miercoles), Periodos.Rango(TipoPeriodo.Dia, miercoles));
        Falla(() => Periodos.Rango(TipoPeriodo.Personalizado, miercoles, miercoles, miercoles.AddDays(-1)), "final");
    }
}

public class AlmacenamientoTests : PruebaBase
{
    [Fact]
    public void Pedidos_simultaneos_no_se_pisan_entre_si()
    {
        CrearCajero("Pedro");
        EntrarComo("Pedro");
        var tpl = Producto(1);

        Parallel.For(0, 20, i => PedidoService.Guardar(new Pedido
        {
            MetodoPago = "Efectivo",
            Cliente = "Cliente " + i,
            TipoPedido = "Mostrador",
            Detalles = { new DetallePedido { MenuItemId = 1, NombreProducto = tpl.Nombre, PrecioUnitario = tpl.Precio, Cantidad = 1 } }
        }));

        var todos = PedidoService.ObtenerTodos();
        Assert.Equal(20, todos.Count);
        Assert.Equal(20, todos.Select(p => p.Id).Distinct().Count());
        Assert.Equal(20, todos.Select(p => p.Folio).Distinct().Count());
    }

    [Fact]
    public void Un_archivo_danado_se_recupera_de_la_copia_anterior_sin_perder_todo()
    {
        CrearCajero("Pedro");
        EntrarComo("Pedro");
        Pedir("Luis", Linea(1, 1));
        Pedir("Maria", Linea(2, 1));

        string ruta = Path.Combine(Datos, "pedidos.json");
        File.WriteAllText(ruta, "{ esto no es json");

        var recuperados = PedidoService.ObtenerTodos();
        Assert.Single(recuperados);                          // la copia .bak tenia el estado anterior
        Assert.Contains(Directory.GetFiles(Datos), f => Path.GetFileName(f).StartsWith("pedidos.json.corrupto-"));
    }

    [Fact]
    public void Un_archivo_danado_sin_copia_avisa_y_no_se_sobrescribe_con_una_lista_vacia()
    {
        CrearCajero("Pedro");
        EntrarComo("Pedro");
        Pedir("Luis", Linea(1, 1));

        string ruta = Path.Combine(Datos, "pedidos.json");
        File.Delete(ruta + ".bak");
        File.WriteAllText(ruta, "{ esto no es json");

        Assert.Throws<DatosCorruptosException>(() => PedidoService.ObtenerTodos());
        Assert.Throws<DatosCorruptosException>(() => Pedir("Maria", Linea(2, 1)));
        Assert.Equal("{ esto no es json", File.ReadAllText(ruta));   // intacto: no se perdio nada mas
    }

    [Fact]
    public void Hash_de_contrasenas_verifica_bien_y_no_repite_la_sal()
    {
        string a = PasswordHasher.Hash("secreto1");
        string b = PasswordHasher.Hash("secreto1");
        Assert.NotEqual(a, b);
        Assert.True(PasswordHasher.Verificar("secreto1", a));
        Assert.False(PasswordHasher.Verificar("secreto2", a));
        Assert.False(PasswordHasher.Verificar("secreto1", "basura"));
        Assert.False(PasswordHasher.Verificar("", a));
    }
}
