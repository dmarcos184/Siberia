using SiberiaApp.Models;
using SiberiaApp.Services;
using Xunit;

namespace SiberiaApp.Tests;

public class PagosDescuentosDevolucionesTests : PruebaBase
{
    public PagosDescuentosDevolucionesTests()
    {
        CrearCajero("Pedro");
        EntrarComo("Pedro");
    }

    private Pedido PedirCon(string cliente, string metodo, decimal descuento, params DetallePedido[] lineas) =>
        PedidoService.Guardar(new Pedido
        {
            Cliente = cliente,
            TipoPedido = "Mostrador",
            MetodoPago = metodo,
            Descuento = descuento,
            Detalles = lineas.ToList()
        });

    private Pedido Entregar(Pedido p) => PedidoService.CambiarEstado(p.Id, EstadosPedido.Entregado);

    // ---------------- Metodo de pago ----------------

    [Fact]
    public void El_metodo_de_pago_es_obligatorio_y_debe_ser_valido_y_se_guarda()
    {
        Falla(() => PedirCon("Luis", "", 0m, Linea(1, 1)), "método de pago");
        Falla(() => PedirCon("Luis", "Cheque", 0m, Linea(1, 1)), "método de pago");

        var p = PedirCon("Luis", MetodosPago.Tarjeta, 0m, Linea(1, 1));
        Assert.Equal("Tarjeta", PedidoService.ObtenerPorId(p.Id).MetodoPago);
        Assert.Contains("Pago: Tarjeta", PedidoService.GenerarTicket(p));
    }

    // ---------------- Descuentos ----------------

    [Fact]
    public void El_descuento_se_valida_y_se_resta_del_total()
    {
        Falla(() => PedirCon("A", MetodosPago.Efectivo, -1m, Linea(1, 2)), "negativo");
        Falla(() => PedirCon("A", MetodosPago.Efectivo, 0.001m, Linea(1, 2)), "centavos");
        Falla(() => PedirCon("A", MetodosPago.Efectivo, 171m, Linea(1, 2)), "mayor al importe");
        Falla(() => PedirCon("A", MetodosPago.Efectivo, 17.01m, Linea(1, 2)), "cajero");   // maximo 10% de 170 = 17.00

        var ok = PedirCon("A", MetodosPago.Efectivo, 17m, Linea(1, 2));
        Assert.Equal(170m, ok.Subtotal);
        Assert.Equal(17m, ok.Descuento);
        Assert.Equal(153m, ok.Total);
        Assert.Contains("DESCUENTO", PedidoService.GenerarTicket(ok));
        Assert.Equal(153m, PedidoService.ObtenerPorId(ok.Id).Total);
    }

    [Fact]
    public void El_total_mostrado_debe_incluir_el_descuento()
    {
        var pedido = new Pedido
        {
            Cliente = "B", TipoPedido = "Mostrador", MetodoPago = MetodosPago.Efectivo,
            Descuento = 10m, Detalles = { Linea(1, 2) }
        };
        Falla(() => PedidoService.Guardar(pedido, 170m), "no coincide");
        Assert.Equal(160m, PedidoService.Guardar(pedido, 160m).Total);
    }

    [Fact]
    public void Un_administrador_puede_dar_mas_descuento_pero_nunca_mas_del_importe()
    {
        EntrarComoAdmin();

        var mitad = PedirCon("A", MetodosPago.Tarjeta, 85m, Linea(1, 2));   // 50%
        Assert.Equal(85m, mitad.Total);

        Falla(() => PedirCon("B", MetodosPago.Tarjeta, 171m, Linea(1, 2)), "mayor al importe");

        var gratis = PedirCon("C", MetodosPago.Efectivo, 85m, Linea(1, 1));
        Assert.Equal(0m, gratis.Total);
    }

    // ---------------- Devoluciones ----------------

    [Fact]
    public void Devoluciones_solo_administrador_solo_entregados_y_sin_pasarse_del_total()
    {
        var pendiente = PedirCon("A", MetodosPago.Efectivo, 0m, Linea(1, 2));    // 170
        var entregado = Entregar(PedirCon("B", MetodosPago.Efectivo, 0m, Linea(6, 2)));   // 130

        Falla(() => PedidoService.RegistrarDevolucion(entregado.Id, 10m, "producto frío"), "administrador");

        EntrarComoAdmin();
        Falla(() => PedidoService.RegistrarDevolucion(pendiente.Id, 10m, "producto frío"), "entregados");
        Falla(() => PedidoService.RegistrarDevolucion(entregado.Id, 0m, "producto frío"), "mayor a cero");
        Falla(() => PedidoService.RegistrarDevolucion(entregado.Id, 10m, "x"), "motivo");
        Falla(() => PedidoService.RegistrarDevolucion(entregado.Id, 131m, "producto frío"), "no puede pasar");
        Falla(() => PedidoService.RegistrarDevolucion(entregado.Id, 10.005m, "producto frío"), "centavos");

        var p1 = PedidoService.RegistrarDevolucion(entregado.Id, 50m, "producto frío");
        Assert.Equal(50m, p1.MontoDevuelto);
        Assert.Equal(80m, p1.TotalNeto);
        Assert.Equal("admin", p1.DevueltoPor);

        var p2 = PedidoService.RegistrarDevolucion(entregado.Id, 80m, "cliente inconforme");
        Assert.Equal(130m, p2.MontoDevuelto);
        Assert.Equal(0m, p2.TotalNeto);
        Assert.Contains("producto frío", p2.MotivoDevolucion);
        Assert.Contains("cliente inconforme", p2.MotivoDevolucion);

        Falla(() => PedidoService.RegistrarDevolucion(entregado.Id, 1m, "otra devolución"), "no puede pasar");
        Falla(() => PedidoService.CambiarEstado(entregado.Id, EstadosPedido.Cancelado), "devolución");
        Assert.Contains("DEVUELTO", PedidoService.GenerarTicket(p2));
    }

    [Fact]
    public void No_se_registran_devoluciones_en_un_dia_con_corte_cerrado_hasta_reabrirlo()
    {
        var p = Entregar(PedirCon("A", MetodosPago.Efectivo, 0m, Linea(1, 1)));   // 85
        CorteService.CerrarCorte(DateTime.Today, null);

        EntrarComoAdmin();
        Falla(() => PedidoService.RegistrarDevolucion(p.Id, 10m, "producto frío"), "corte");

        CorteService.ReabrirCorte(CorteService.ObtenerCortes().Single().Id, "devolución pendiente");
        PedidoService.RegistrarDevolucion(p.Id, 10m, "producto frío");

        var recerrado = CorteService.CerrarCorte(DateTime.Today, null, p.UsuarioId);
        Assert.Equal(10m, recerrado.TotalDevoluciones);
        Assert.Equal(75m, recerrado.TotalNeto);
    }

    // ---------------- Corte y reportes por forma de pago ----------------

    private (Pedido a, Pedido b, Pedido c) TresVentas()
    {
        var a = Entregar(PedirCon("A", MetodosPago.Efectivo, 0m, Linea(1, 2)));          // 170
        var b = Entregar(PedirCon("B", MetodosPago.Tarjeta, 13m, Linea(6, 2)));          // 130 - 13 = 117
        var c = Entregar(PedirCon("C", MetodosPago.Transferencia, 0m, Linea(2, 2)));     // 150
        return (a, b, c);
    }

    [Fact]
    public void El_corte_separa_el_ingreso_por_forma_de_pago_y_cuadra_con_el_neto()
    {
        var (a, _, _) = TresVentas();
        EntrarComoAdmin();
        PedidoService.RegistrarDevolucion(a.Id, 20m, "producto frío");

        EntrarComo("Pedro");
        var corte = CorteService.CerrarCorte(DateTime.Today, null);

        Assert.Equal(437m, corte.TotalVentas);          // 170 + 117 + 150
        Assert.Equal(20m, corte.TotalDevoluciones);
        Assert.Equal(13m, corte.TotalDescuentos);
        Assert.Equal(417m, corte.TotalNeto);

        Assert.Equal(150m, corte.VentasEfectivo);       // 170 - 20 devueltos
        Assert.Equal(117m, corte.VentasTarjeta);
        Assert.Equal(150m, corte.VentasTransferencia);
        Assert.Equal(0m, corte.VentasSinMetodo);
        Assert.Equal(corte.TotalNeto,
            corte.VentasEfectivo + corte.VentasTarjeta + corte.VentasTransferencia + corte.VentasSinMetodo);

        string texto = CorteService.GenerarTexto(corte);
        Assert.Contains("Efectivo", texto);
        Assert.Contains("Transferencia", texto);
        Assert.DoesNotContain("no los maneja", texto);
    }

    [Fact]
    public void Los_reportes_separan_por_metodo_y_los_pedidos_anteriores_quedan_sin_registrar()
    {
        var (a, _, c) = TresVentas();

        // simula un pedido de la version anterior: sin metodo de pago
        var lista = PedidoService.CargarInterno();
        lista.First(p => p.Id == c.Id).MetodoPago = string.Empty;
        PedidoService.GuardarInterno(lista);

        EntrarComoAdmin();
        PedidoService.RegistrarDevolucion(a.Id, 20m, "producto frío");

        var r = ReporteService.ObtenerResumen(DateTime.Today, DateTime.Today);
        Assert.Equal(417m, r.Ventas);
        Assert.Equal(20m, r.Devoluciones);
        Assert.Equal(13m, r.Descuentos);
        Assert.Equal(150m, r.VentasEfectivo);
        Assert.Equal(117m, r.VentasTarjeta);
        Assert.Equal(0m, r.VentasTransferencia);
        Assert.Equal(150m, r.VentasSinMetodo);
        Assert.Equal(r.Ventas, r.VentasEfectivo + r.VentasTarjeta + r.VentasTransferencia + r.VentasSinMetodo);
        Assert.Contains("Sin registrar", r.TextoPorMetodo);
        Assert.Equal("Sin registrar", PedidoService.ObtenerPorId(c.Id).MetodoPagoTexto);
    }
}
