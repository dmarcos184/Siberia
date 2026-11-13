using SiberiaApp.Models;
using SiberiaApp.Services;
using Xunit;

namespace SiberiaApp.Tests;

public class CortesTests : PruebaBase
{
    private readonly Usuario _pedro;
    private readonly Usuario _ana;

    public CortesTests()
    {
        _pedro = CrearCajero("Pedro");
        _ana = CrearCajero("Ana");
    }

    [Fact]
    public void Un_usuario_cierra_su_propio_corte_y_los_totales_coinciden_con_sus_pedidos()
    {
        EntrarComo("Pedro");
        var a = PedirYEntregar("Luis", Linea(1, 2));        // 170
        var b = PedirYEntregar("Maria", Linea(6, 1));       // 65
        var c = Pedir("Sofia", Linea(2, 1));                // 75 -> se cancela
        PedidoService.CambiarEstado(c.Id, EstadosPedido.Cancelado);

        var corte = CorteService.CerrarCorte(DateTime.Today, "todo bien");

        Assert.Equal(Corte.Cerrado, corte.Estado);
        Assert.Equal(_pedro.Id, corte.UsuarioId);
        Assert.Equal("Pedro", corte.NombreUsuario);
        Assert.Equal(2, corte.TotalPedidos);
        Assert.Equal(1, corte.PedidosCancelados);
        Assert.Equal(310m, corte.TotalVentas);       // 170 + 65 + 75 registrados
        Assert.Equal(75m, corte.MontoCancelado);
        Assert.Equal(235m, corte.TotalNeto);          // lo cancelado no es ingreso
        Assert.Equal("todo bien", corte.Observaciones);
        Assert.Equal("Pedro", corte.CerradoPor);
        Assert.Equal(3, corte.PedidoIds.Count);

        // los pedidos quedan asociados al corte
        Assert.All(PedidoService.ObtenerTodos(), p => Assert.Equal(corte.Id, p.CorteId));

        // y el neto coincide con la suma de pedidos no cancelados
        Assert.Equal(PedidoService.ObtenerTodos().Where(p => !p.EsCancelado).Sum(p => p.Total), corte.TotalNeto);
    }

    [Fact]
    public void No_se_puede_cerrar_un_corte_con_pedidos_sin_finalizar()
    {
        EntrarComo("Pedro");
        PedirYEntregar("Luis", Linea(1, 1));
        Pedir("Maria", Linea(2, 1)); // sigue Pendiente

        Falla(() => CorteService.CerrarCorte(DateTime.Today, null), "sin finalizar");
        var dia = Assert.Single(CorteService.ObtenerDiasPendientes());
        Assert.False(dia.ListoParaCerrar);
    }

    [Fact]
    public void No_se_puede_cortar_un_dia_sin_pedidos_ni_una_fecha_futura()
    {
        EntrarComo("Pedro");
        PedirYEntregar("Luis", Linea(1, 1));

        Falla(() => CorteService.CerrarCorte(DateTime.Today.AddDays(-3), null), "días trabajados");
        Falla(() => CorteService.Previsualizar(DateTime.Today.AddDays(-3)), "días trabajados");
        Falla(() => CorteService.CerrarCorte(DateTime.Today.AddDays(1), null), "futura");
        Assert.Empty(CorteService.ObtenerCortes());
    }

    [Fact]
    public void Un_corte_cerrado_no_se_puede_duplicar()
    {
        EntrarComo("Pedro");
        PedirYEntregar("Luis", Linea(1, 1));

        CorteService.CerrarCorte(DateTime.Today, null);
        Falla(() => CorteService.CerrarCorte(DateTime.Today, null), "ya está cerrado");
        Assert.Single(CorteService.ObtenerCortes());
        Assert.Empty(CorteService.ObtenerDiasPendientes());
    }

    [Fact]
    public void Un_usuario_normal_no_puede_hacer_el_corte_de_otro_empleado()
    {
        EntrarComo("Ana");
        PedirYEntregar("Luis", Linea(1, 1));

        EntrarComo("Pedro");
        PedirYEntregar("Maria", Linea(2, 1));

        // Pedro intenta cortar el dia de Ana
        Falla(() => CorteService.CerrarCorte(DateTime.Today, null, _ana.Id), "propios días");
        Falla(() => CorteService.Previsualizar(DateTime.Today, _ana.Id), "propios días");
        Falla(() => CorteService.ObtenerDiasPendientes(_ana.Id), "propia información");

        // su corte solo incluye SUS ventas
        var corte = CorteService.CerrarCorte(DateTime.Today, null);
        Assert.Equal(75m, corte.TotalVentas);
        Assert.Equal(_pedro.Id, corte.UsuarioId);

        // el corte de Ana sigue pendiente
        EntrarComoAdmin();
        Assert.Contains(CorteService.ObtenerDiasPendientes(), d => d.UsuarioId == _ana.Id);
    }

    [Fact]
    public void Cada_usuario_ve_solo_sus_cortes_y_el_administrador_ve_todos_con_filtros()
    {
        EntrarComo("Pedro");
        var p1 = PedirYEntregar("Luis", Linea(1, 1));
        MoverPedidoAAyer(p1.Id);
        CorteService.CerrarCorte(DateTime.Today.AddDays(-1), null);
        PedirYEntregar("Maria", Linea(2, 1));
        CorteService.CerrarCorte(DateTime.Today, null);

        EntrarComo("Ana");
        PedirYEntregar("Sofia", Linea(5, 1));
        CorteService.CerrarCorte(DateTime.Today, null);

        // Ana: solo el suyo
        var deAna = CorteService.ObtenerCortes();
        Assert.Single(deAna);
        Assert.All(deAna, c => Assert.Equal(_ana.Id, c.UsuarioId));
        Falla(() => CorteService.ObtenerCortes(null, null, _pedro.Id), "propia información");
        Assert.Null(CorteService.ObtenerCorte(CorteService.CargarInterno().First(c => c.UsuarioId == _pedro.Id).Id));

        // Administrador: todos, filtrables por usuario y fecha
        EntrarComoAdmin();
        Assert.Equal(3, CorteService.ObtenerCortes().Count);
        Assert.Equal(2, CorteService.ObtenerCortes(null, null, _pedro.Id).Count);
        Assert.Single(CorteService.ObtenerCortes(DateTime.Today, DateTime.Today, _pedro.Id));
        Assert.Equal(2, CorteService.ObtenerCortes(DateTime.Today, DateTime.Today).Count);
    }

    [Fact]
    public void Un_corte_cerrado_protege_las_ventas_de_ese_dia()
    {
        EntrarComo("Pedro");
        var p = PedirYEntregar("Luis", Linea(1, 1));
        CorteService.CerrarCorte(DateTime.Today, null);

        // no se pueden registrar mas ventas hoy ni cambiar las que ya hay
        Falla(() => Pedir("Maria", Linea(2, 1)), "corte");
        Falla(() => PedidoService.CambiarEstado(p.Id, EstadosPedido.Cancelado), "corte");

        // ni siquiera el administrador, hasta reabrir el corte
        EntrarComoAdmin();
        Falla(() => PedidoService.CambiarEstado(p.Id, EstadosPedido.Cancelado), "reabrir");

        var corte = CorteService.ObtenerCortes().Single();
        EntrarComo("Pedro");
        Falla(() => CorteService.ReabrirCorte(corte.Id, "quiero cambiarlo"), "administrador");

        EntrarComoAdmin();
        Falla(() => CorteService.ReabrirCorte(corte.Id, "x"), "motivo");
        var reabierto = CorteService.ReabrirCorte(corte.Id, "se capturo mal un pedido");
        Assert.Equal(Corte.Abierto, reabierto.Estado);
        Assert.Equal("admin", reabierto.ReabiertoPor);
        Assert.Equal("se capturo mal un pedido", reabierto.MotivoReapertura);

        // ahora si se puede corregir y volver a cerrar (el corte conserva su historia)
        PedidoService.CambiarEstado(p.Id, EstadosPedido.Cancelado);
        var recerrado = CorteService.CerrarCorte(DateTime.Today, "corregido", _pedro.Id);
        Assert.Equal(corte.Id, recerrado.Id);
        Assert.Equal(Corte.Cerrado, recerrado.Estado);
        Assert.Equal(0m, recerrado.TotalNeto);
        Assert.Equal("admin", recerrado.CerradoPor);
        Assert.Equal("se capturo mal un pedido", recerrado.MotivoReapertura);
        Assert.Single(CorteService.ObtenerCortes());
    }

    [Fact]
    public void El_texto_del_corte_incluye_los_pedidos_con_sus_notas()
    {
        EntrarComo("Pedro");
        PedirYEntregar("Luis", Linea(1, 4, (1, "sin crema")));
        var corte = CorteService.CerrarCorte(DateTime.Today, null);

        string texto = CorteService.GenerarTexto(corte, CorteService.ObtenerPedidosDelCorte(corte.Id));
        Assert.Contains("CERRADO", texto);
        Assert.Contains("1 sin crema", texto);
        Assert.Contains("no calculable", texto, StringComparison.OrdinalIgnoreCase); // no hay costos capturados
    }

    [Fact]
    public void Los_dias_anteriores_sin_cerrar_cuentan_como_cortes_pendientes()
    {
        EntrarComo("Pedro");
        var p = PedirYEntregar("Luis", Linea(1, 1));
        MoverPedidoAAyer(p.Id);
        PedirYEntregar("Maria", Linea(2, 1)); // hoy: aun no es "pendiente"

        EntrarComoAdmin();
        Assert.Equal(1, CorteService.ContarCortesPendientes());
    }
}
