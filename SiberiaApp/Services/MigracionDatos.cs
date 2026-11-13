using System;
using System.Linq;
using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Pone al dia los .json de versiones anteriores SIN perder nada. Se ejecuta al abrir la app
    /// y se puede ejecutar varias veces (no repite trabajo):
    ///  1. Contraseñas en texto plano -> contraseñas protegidas (hash).
    ///  2. Pedidos viejos -> se les asigna el UsuarioId buscando el nombre de quien los capturo.
    ///  3. Se crea el administrador inicial si todavia no hay usuarios.
    /// Los campos nuevos (notas, costos, estado de usuario...) no necesitan migracion:
    /// los registros viejos simplemente los traen vacios o con su valor por defecto.
    /// </summary>
    public static class MigracionDatos
    {
        public static void Ejecutar()
        {
            JsonDataService.Transaccion(() =>
            {
                // ---- 1) Usuarios ----
                var usuarios = UsuarioService.CargarInterno(); // crea al admin si no habia usuarios
                bool cambiosUsuarios = false;
                foreach (var u in usuarios)
                {
                    if (string.IsNullOrEmpty(u.PasswordHash) && !string.IsNullOrEmpty(u.Password))
                    {
                        u.PasswordHash = PasswordHasher.Hash(u.Password);
                        u.Password = null;
                        cambiosUsuarios = true;
                    }
                    else if (u.Password != null)
                    {
                        u.Password = null; // ya tenia hash: se borra cualquier texto plano que haya quedado
                        cambiosUsuarios = true;
                    }

                    if (string.IsNullOrWhiteSpace(u.Rol))
                    {
                        u.Rol = Roles.Cajero;
                        cambiosUsuarios = true;
                    }
                }
                if (cambiosUsuarios)
                    UsuarioService.GuardarInterno(usuarios);

                // ---- 2) Pedidos: ligar al usuario que los capturo ----
                var pedidos = PedidoService.CargarInterno();
                bool cambiosPedidos = false;
                foreach (var p in pedidos)
                {
                    if (p.UsuarioId != 0 || string.IsNullOrWhiteSpace(p.UsuarioCreo)) continue;

                    var duenio = usuarios.FirstOrDefault(u =>
                        u.NombreUsuario.Equals(p.UsuarioCreo, StringComparison.OrdinalIgnoreCase));
                    if (duenio == null) continue;

                    p.UsuarioId = duenio.Id;
                    cambiosPedidos = true;
                }
                if (cambiosPedidos)
                    PedidoService.GuardarInterno(pedidos);

                return true;
            });
        }
    }
}
