using Newtonsoft.Json;
using SiberiaApp.Models;
using SiberiaApp.Services;
using Xunit;

namespace SiberiaApp.Tests;

public class UsuariosTests : PruebaBase
{
    [Fact]
    public void Admin_puede_crear_editar_y_desactivar_usuarios()
    {
        EntrarComoAdmin();

        var u = UsuarioService.Crear("Maria", Clave, Roles.Cajero);
        Assert.Contains(UsuarioService.ObtenerTodos(), x => x.NombreUsuario == "Maria");

        UsuarioService.Actualizar(u.Id, "Maria Lopez", Roles.Cajero, true);
        Assert.Contains(UsuarioService.ObtenerTodos(), x => x.NombreUsuario == "Maria Lopez");

        UsuarioService.Actualizar(u.Id, "Maria Lopez", Roles.Cajero, false);
        SesionService.Cerrar();
        Falla(() => UsuarioService.Autenticar("Maria Lopez", Clave), "desactivada");
    }

    [Fact]
    public void Usuario_normal_no_puede_administrar_cuentas_ni_el_menu()
    {
        CrearCajero("Pedro");
        var ana = CrearCajero("Ana");
        EntrarComo("Pedro");

        Falla(() => UsuarioService.Crear("Hacker", Clave, Roles.Administrador), "administrador");
        Falla(() => UsuarioService.ObtenerTodos());
        Falla(() => UsuarioService.Actualizar(ana.Id, "Ana", Roles.Administrador, true));
        Falla(() => UsuarioService.Eliminar(ana.Id));
        Falla(() => UsuarioService.RestablecerPassword(ana.Id, "nueva123"));
        Falla(() => MenuService.Agregar(new MenuItem { Nombre = "Nuevo", Categoria = "X", Precio = 10 }));
        Falla(() => MenuService.Actualizar(new MenuItem { Id = 1, Nombre = "Tostada Especial", Categoria = "Tostada", Precio = 1 }));
        Falla(() => ReporteService.ObtenerPanelAdmin());
    }

    [Fact]
    public void Operacion_protegida_sin_sesion_se_rechaza()
    {
        SesionService.Cerrar();
        Falla(() => PedidoService.ObtenerTodos(), "iniciar sesión");
        Falla(() => CorteService.ObtenerCortes(), "iniciar sesión");
        Falla(() => UsuarioService.ObtenerTodos(), "iniciar sesión");
    }

    [Fact]
    public void Un_cambio_de_rol_o_una_cuenta_desactivada_se_aplica_aunque_la_sesion_siga_abierta()
    {
        var pedro = CrearCajero("Pedro");
        EntrarComo("Pedro");              // la sesion guarda a Pedro como cajero activo

        EntrarComoAdmin();
        UsuarioService.Actualizar(pedro.Id, "Pedro", Roles.Cajero, false);

        // se vuelve a la sesion "vieja" de Pedro (copia en memoria que dice Activo = true)
        SesionService.Iniciar(pedro);
        Falla(() => PedidoService.ObtenerTodos(), "ya no está activa");
    }

    [Fact]
    public void No_se_puede_desactivar_ni_eliminar_al_ultimo_administrador_ni_a_uno_mismo()
    {
        var admin = EntrarComoAdmin();

        Falla(() => UsuarioService.Actualizar(admin.Id, "admin", Roles.Administrador, false), "propia cuenta");
        Falla(() => UsuarioService.Actualizar(admin.Id, "admin", Roles.Cajero, true), "administrador");
        Falla(() => UsuarioService.Eliminar(admin.Id), "propia cuenta");

        // con un segundo admin si se puede quitar a otro, pero nunca dejar el sistema sin administrador activo
        var otro = UsuarioService.Crear("Jefe2", Clave, Roles.Administrador);
        UsuarioService.Actualizar(otro.Id, "Jefe2", Roles.Cajero, true);
        Assert.Equal(Roles.Cajero, UsuarioService.ObtenerTodos().First(u => u.Id == otro.Id).Rol);
    }

    [Fact]
    public void Un_usuario_con_historial_no_se_elimina_pero_uno_sin_historial_si()
    {
        var pedro = CrearCajero("Pedro");
        var sinHistorial = CrearCajero("Nuevo");
        EntrarComo("Pedro");
        Pedir("Cliente", Linea(1, 1));

        EntrarComoAdmin();
        Falla(() => UsuarioService.Eliminar(pedro.Id), "historial");
        UsuarioService.Eliminar(sinHistorial.Id);
        Assert.DoesNotContain(UsuarioService.ObtenerTodos(), u => u.Id == sinHistorial.Id);
    }

    [Theory]
    [InlineData("", "clave123")]
    [InlineData("ab", "clave123")]
    [InlineData("Juan", "123")]
    [InlineData("Juan", "con espacio")]
    [InlineData("Juan", "Juan")]
    [InlineData("Ju@n!", "clave123")]
    public void Datos_de_usuario_invalidos_se_rechazan(string nombre, string password)
    {
        EntrarComoAdmin();
        Falla(() => UsuarioService.Crear(nombre, password, Roles.Cajero));
    }

    [Fact]
    public void No_se_permiten_usuarios_repetidos_sin_importar_mayusculas()
    {
        EntrarComoAdmin();
        UsuarioService.Crear("Maria", Clave, Roles.Cajero);
        Falla(() => UsuarioService.Crear("MARIA", Clave, Roles.Cajero), "ya existe");
    }

    [Fact]
    public void Las_contrasenas_se_guardan_con_hash_nunca_en_texto_plano()
    {
        CrearCajero("Pedro");
        string json = File.ReadAllText(Path.Combine(Datos, "usuarios.json"));
        Assert.DoesNotContain(Clave, json);
        Assert.DoesNotContain("admin123", json);
        Assert.Contains("PBKDF2$", json);
        Assert.Null(UsuarioService.Autenticar("Pedro", "incorrecta"));
    }

    [Fact]
    public void Restablecer_contrasena_obliga_a_cambiarla_y_la_nueva_funciona()
    {
        var pedro = CrearCajero("Pedro");
        EntrarComoAdmin();
        UsuarioService.RestablecerPassword(pedro.Id, "temporal99");

        var u = UsuarioService.Autenticar("Pedro", "temporal99");
        Assert.NotNull(u);
        Assert.True(u.DebeCambiarPassword);

        SesionService.Iniciar(u);
        UsuarioService.EstablecerPasswordPropia("MiClave2026");
        SesionService.Cerrar();

        var otraVez = UsuarioService.Autenticar("Pedro", "MiClave2026");
        Assert.NotNull(otraVez);
        Assert.False(otraVez.DebeCambiarPassword);
        Assert.Null(UsuarioService.Autenticar("Pedro", "temporal99"));
    }

    [Fact]
    public void Tras_varios_intentos_fallidos_la_cuenta_se_bloquea_un_momento()
    {
        CrearCajero("Pedro");
        for (int i = 0; i < 5; i++)
            Assert.Null(UsuarioService.Autenticar("Pedro", "mal" + i));

        Falla(() => UsuarioService.Autenticar("Pedro", Clave), "intentos");
    }

    [Fact]
    public void Los_archivos_de_la_version_anterior_se_migran_sin_perder_datos()
    {
        // usuarios.json y pedidos.json "viejos": contraseña en texto plano, sin Activo, sin UsuarioId, sin notas
        File.WriteAllText(Path.Combine(Datos, "usuarios.json"),
            "[{\"Id\":1,\"NombreUsuario\":\"admin\",\"Password\":\"vieja123\",\"Rol\":\"Administrador\"}," +
            "{\"Id\":2,\"NombreUsuario\":\"caja1\",\"Password\":\"pass456\",\"Rol\":\"Cajero\"}]");
        File.WriteAllText(Path.Combine(Datos, "pedidos.json"),
            "[{\"Id\":1,\"Folio\":\"SIB-20260101-0001\",\"Cliente\":\"Luis\",\"TipoPedido\":\"Mostrador\"," +
            "\"FechaHora\":\"2026-01-01T12:00:00\",\"Estado\":\"Entregado\",\"UsuarioCreo\":\"caja1\"," +
            "\"Detalles\":[{\"MenuItemId\":1,\"NombreProducto\":\"Tostada Especial\",\"PrecioUnitario\":85.0,\"Cantidad\":2,\"Subtotal\":170.0}]," +
            "\"Total\":170.0}]");

        MigracionDatos.Ejecutar();
        MigracionDatos.Ejecutar(); // se puede repetir sin danar nada

        string json = File.ReadAllText(Path.Combine(Datos, "usuarios.json"));
        Assert.DoesNotContain("vieja123", json);
        Assert.DoesNotContain("pass456", json);

        var admin = UsuarioService.Autenticar("admin", "vieja123");
        Assert.NotNull(admin);
        Assert.True(admin.Activo);
        SesionService.Iniciar(admin);

        var pedidos = PedidoService.ObtenerTodos();
        var p = Assert.Single(pedidos);
        Assert.Equal(2, p.UsuarioId);                 // se ligo a "caja1"
        Assert.Equal(170m, p.Total);
        Assert.NotNull(p.Detalles[0].Notas);          // sin notas, pero no nulo
        Assert.Empty(p.Detalles[0].Notas);
        Assert.Equal("SIB-20260101-0001", p.Folio);
        Assert.Equal("Sin registrar", p.MetodoPagoTexto);   // no se le inventa un metodo de pago
        Assert.Equal(0m, p.Descuento);
        Assert.Equal(0m, p.MontoDevuelto);
    }
}
