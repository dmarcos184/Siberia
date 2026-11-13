using SiberiaApp.Models;
using SiberiaApp.Services;
using Xunit;

namespace SiberiaApp.Tests;

public class PedidosTests : PruebaBase
{
    public PedidosTests()
    {
        CrearCajero("Pedro");
        EntrarComo("Pedro");
    }

    [Fact]
    public void Se_agregan_productos_y_el_total_se_calcula_en_el_servicio()
    {
        // Tostada Especial 85 x4 + Taco Sencillo 65 x2 = 340 + 130
        var p = Pedir("Luis", Linea(1, 4), Linea(6, 2));

        Assert.Equal(470m, p.Total);
        Assert.Equal(EstadosPedido.Pendiente, p.Estado);
        Assert.Equal("Pedro", p.UsuarioCreo);
        Assert.NotEqual(0, p.UsuarioId);
        Assert.StartsWith("SIB-", p.Folio);
    }

    [Fact]
    public void Los_importes_con_centavos_no_pierden_precision()
    {
        EntrarComoAdmin();
        MenuService.Agregar(new MenuItem { Nombre = "Agua", Categoria = "Bebida", Precio = 10.10m });
        int id = MenuService.ObtenerTodos().First(m => m.Nombre == "Agua").Id;
        EntrarComo("Pedro");

        var p = Pedir("Ana", Linea(id, 3));
        Assert.Equal(30.30m, p.Total);
    }

    [Fact]
    public void El_total_mostrado_en_pantalla_debe_coincidir_con_el_calculado()
    {
        var pedido = new Pedido { MetodoPago = "Efectivo", Cliente = "Luis", Detalles = { Linea(1, 2) } };
        Falla(() => PedidoService.Guardar(pedido, 1m), "no coincide");
        Assert.Empty(PedidoService.ObtenerTodos());

        var ok = PedidoService.Guardar(pedido, 170m);
        Assert.Equal(170m, ok.Total);
    }

    [Fact]
    public void No_se_permiten_pedidos_invalidos()
    {
        Falla(() => Pedir("Luis"), "al menos un producto");
        Falla(() => Pedir("", Linea(1, 1)), "cliente");
        Falla(() => Pedir("   ", Linea(1, 1)), "cliente");

        Falla(() => Pedir("Luis", Linea(1, 0)), "mayor a cero");
        Falla(() => Pedir("Luis", Linea(1, -3)), "mayor a cero");
        Falla(() => Pedir("Luis", Linea(1, 11)), "entre 1 y");

        var inexistente = Linea(1, 1);
        inexistente.MenuItemId = 999;
        Falla(() => Pedir("Luis", inexistente), "ya no existe");

        var negativo = Linea(1, 1);
        negativo.PrecioUnitario = -5m;
        Falla(() => Pedir("Luis", negativo), "negativo");

        Assert.Empty(PedidoService.ObtenerTodos());
    }

    [Fact]
    public void Un_mismo_producto_no_puede_pasar_del_maximo_aunque_venga_en_dos_renglones()
    {
        Falla(() => Pedir("Luis", Linea(1, 6), Linea(1, 6)), "hasta 10");
    }

    [Fact]
    public void Un_pedido_a_domicilio_exige_direccion_y_telefono_validos()
    {
        var sinDatos = new Pedido { MetodoPago = "Efectivo", Cliente = "Luis", TipoPedido = "Domicilio", Detalles = { Linea(1, 1) } };
        Falla(() => PedidoService.Guardar(sinDatos), "dirección y teléfono");

        var telMalo = new Pedido { MetodoPago = "Efectivo", Cliente = "Luis", TipoPedido = "Domicilio", Direccion = "Calle 1", Telefono = "abc", Detalles = { Linea(1, 1) } };
        Falla(() => PedidoService.Guardar(telMalo), "teléfono");

        var bien = new Pedido { MetodoPago = "Efectivo", Cliente = "Luis", TipoPedido = "Domicilio", Direccion = "Calle 1", Telefono = "81 1234 5678", Detalles = { Linea(1, 1) } };
        Assert.Equal("Domicilio", PedidoService.Guardar(bien).TipoPedido);
    }

    [Fact]
    public void Producto_desactivado_no_se_puede_vender_y_precio_cambiado_se_detecta()
    {
        var linea = Linea(1, 1);

        EntrarComoAdmin();
        var item = Producto(1);
        item.Precio = 90m;
        MenuService.Actualizar(item);
        EntrarComo("Pedro");

        Falla(() => Pedir("Luis", linea), "cambió"); // la linea todavia dice 85

        EntrarComoAdmin();
        item.Activo = false;
        MenuService.Actualizar(item);
        EntrarComo("Pedro");
        Falla(() => Pedir("Luis", Linea2(1)), "desactivado");
    }

    private DetallePedido Linea2(int id) => new DetallePedido { MenuItemId = id, NombreProducto = "x", PrecioUnitario = 90m, Cantidad = 1 };

    [Fact]
    public void Un_pedido_identico_enviado_dos_veces_seguidas_se_rechaza()
    {
        Pedir("Luis", Linea(1, 2));
        Falla(() => Pedir("Luis", Linea(1, 2)), "idéntico");
        Assert.Single(PedidoService.ObtenerTodos());

        // otro cliente o distinto contenido si se permite
        Pedir("Maria", Linea(1, 2));
        Pedir("Luis", Linea(1, 3));
        Assert.Equal(3, PedidoService.ObtenerTodos().Count);
    }

    // ---------------- Notas por producto ----------------

    [Fact]
    public void Cada_producto_guarda_sus_propias_notas_y_permanecen_en_el_historial()
    {
        var guardado = Pedir("Luis",
            Linea(1, 4, (1, "sin crema"), (2, "con extra de aguacate")),
            Linea(5, 2),
            Linea(6, 1, (1, "sin salsa")));

        // se vuelve a leer del archivo, como al consultar un pedido anterior
        var leido = PedidoService.ObtenerPorId(guardado.Id);
        Assert.NotNull(leido);

        var tostadas = leido.Detalles.First(d => d.MenuItemId == 1);
        Assert.Equal(2, tostadas.Notas.Count);
        Assert.Equal("1 sin crema", tostadas.Notas[0].Descripcion);
        Assert.Equal("2 con extra de aguacate", tostadas.Notas[1].Descripcion);

        Assert.Empty(leido.Detalles.First(d => d.MenuItemId == 5).Notas);
        Assert.Equal("1 sin salsa", leido.Detalles.First(d => d.MenuItemId == 6).ResumenNotas);

        string ticket = PedidoService.GenerarTicket(leido);
        Assert.Contains("1 sin crema", ticket);
        Assert.Contains("2 con extra de aguacate", ticket);
        Assert.Contains("1 sin salsa", ticket);

        Assert.Contains("(1 sin crema", FilaPedido.Desde(leido).Productos);
    }

    [Fact]
    public void Las_notas_invalidas_se_rechazan()
    {
        Falla(() => Pedir("Luis", Linea(1, 2, (3, "sin crema"))), "lleva 2");   // aplica a mas unidades de las que hay
        Falla(() => Pedir("Luis", Linea(1, 2, (0, "sin crema"))), "unidad");    // cero unidades
        Falla(() => Pedir("Luis", Linea(1, 2, (1, "   "))), "vacía");
        Falla(() => Pedir("Luis", Linea(1, 2, (1, new string('x', 61)))), "caracteres");

        var muchas = Enumerable.Range(1, 11).Select(i => (1, "nota " + i)).ToArray();
        Falla(() => Pedir("Luis", Linea(1, 2, muchas)), "más de");
        Assert.Empty(PedidoService.ObtenerTodos());
    }

    // ---------------- Estados y permisos ----------------

    [Fact]
    public void Un_pedido_finalizado_solo_lo_modifica_un_administrador()
    {
        var p = PedirYEntregar("Luis", Linea(1, 1));
        Assert.Equal(EstadosPedido.Entregado, p.Estado);

        Falla(() => PedidoService.CambiarEstado(p.Id, EstadosPedido.Pendiente), "administrador");

        EntrarComoAdmin();
        var modificado = PedidoService.CambiarEstado(p.Id, EstadosPedido.Pendiente);
        Assert.Equal(EstadosPedido.Pendiente, modificado.Estado);
    }

    [Fact]
    public void Cambiar_al_mismo_estado_o_a_uno_inexistente_se_rechaza()
    {
        var p = Pedir("Luis", Linea(1, 1));
        Falla(() => PedidoService.CambiarEstado(p.Id, EstadosPedido.Pendiente), "ya se encuentra");
        Falla(() => PedidoService.CambiarEstado(p.Id, "Volando"), "estado válido");
        Falla(() => PedidoService.CambiarEstado(9999, EstadosPedido.Entregado), "no existe");
    }

    [Fact]
    public void Un_usuario_no_puede_modificar_pedidos_de_otro()
    {
        var pedro = Pedir("Luis", Linea(1, 1));
        CrearCajero("Ana");
        EntrarComo("Ana");

        Falla(() => PedidoService.CambiarEstado(pedro.Id, EstadosPedido.Cancelado), "otro usuario");
        Assert.Empty(PedidoService.ObtenerTodos());
        Assert.Null(PedidoService.ObtenerPorId(pedro.Id));
    }

    [Fact]
    public void Un_producto_vendido_no_se_elimina_del_menu_pero_si_se_desactiva()
    {
        Pedir("Luis", Linea(1, 1));
        EntrarComoAdmin();

        Falla(() => MenuService.Eliminar(1), "historial");

        var item = Producto(1);
        item.Activo = false;
        MenuService.Actualizar(item);
        Assert.False(Producto(1).Activo);

        // el historico conserva el nombre y el precio con que se vendio
        Assert.Equal("Tostada Especial", PedidoService.ObtenerTodos().Single().Detalles.Single().NombreProducto);
    }

    [Fact]
    public void Menu_valida_precios_costos_y_nombres_repetidos()
    {
        EntrarComoAdmin();
        Falla(() => MenuService.Agregar(new MenuItem { Nombre = "X", Categoria = "C", Precio = -1 }), "mayor a cero");
        Falla(() => MenuService.Agregar(new MenuItem { Nombre = "X", Categoria = "C", Precio = 0 }), "mayor a cero");
        Falla(() => MenuService.Agregar(new MenuItem { Nombre = "X", Categoria = "C", Precio = 10, Costo = -1 }), "costo");
        Falla(() => MenuService.Agregar(new MenuItem { Nombre = "", Categoria = "C", Precio = 10 }), "nombre");
        Falla(() => MenuService.Agregar(new MenuItem { Nombre = "tostada especial", Categoria = "C", Precio = 10 }), "ya existe");
    }
}
