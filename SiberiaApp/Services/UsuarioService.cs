using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Alta, edicion, activacion y baja de usuarios. Todo lo administrativo exige rol Administrador
    /// y se vuelve a comprobar aqui (no solo en la pantalla).
    /// </summary>
    public static class UsuarioService
    {
        private const string Archivo = "usuarios.json";

        public const int LongitudMinimaPassword = 6;
        public const int LongitudMaximaPassword = 64;
        private const int MaxIntentosFallidos = 5;
        private static readonly TimeSpan TiempoBloqueo = TimeSpan.FromSeconds(30);

        // intentos fallidos de login por usuario (en memoria)
        private static readonly Dictionary<string, (int Fallos, DateTime Hasta)> Bloqueos =
            new Dictionary<string, (int Fallos, DateTime Hasta)>(StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------------
        // Acceso interno (sin permisos): lo usan SesionService, el login y otros servicios
        // ---------------------------------------------------------------
        internal static List<Usuario> CargarInterno()
        {
            var lista = JsonDataService.Cargar<Usuario>(Archivo);
            if (lista.Count > 0) return lista;

            // Primera vez: se crea el administrador inicial (debe cambiar su contraseña al entrar).
            return JsonDataService.Transaccion(() =>
            {
                var actual = JsonDataService.Cargar<Usuario>(Archivo);
                if (actual.Count > 0) return actual;

                actual.Add(new Usuario
                {
                    Id = 1,
                    NombreUsuario = "admin",
                    PasswordHash = PasswordHasher.Hash("admin123"),
                    Rol = Roles.Administrador,
                    Activo = true,
                    DebeCambiarPassword = true
                });
                JsonDataService.Guardar(Archivo, actual);
                return actual;
            });
        }

        internal static void GuardarInterno(List<Usuario> lista) => JsonDataService.Guardar(Archivo, lista);

        internal static Usuario ObtenerPorIdInterno(int id) =>
            CargarInterno().FirstOrDefault(u => u.Id == id);

        // ---------------------------------------------------------------
        // Login
        // ---------------------------------------------------------------

        /// <summary>
        /// Devuelve el usuario si usuario y contraseña son correctos; null si no.
        /// Lanza ReglaNegocioException si la cuenta esta desactivada o hay demasiados intentos fallidos.
        /// </summary>
        public static Usuario Autenticar(string nombreUsuario, string password)
        {
            nombreUsuario = (nombreUsuario ?? string.Empty).Trim();
            if (nombreUsuario.Length == 0 || string.IsNullOrEmpty(password))
                return null;

            lock (Bloqueos)
            {
                if (Bloqueos.TryGetValue(nombreUsuario, out var b) && b.Hasta > DateTime.Now)
                {
                    int segundos = (int)Math.Ceiling((b.Hasta - DateTime.Now).TotalSeconds);
                    throw new ReglaNegocioException(
                        $"Demasiados intentos fallidos. Espera {segundos} segundos e intenta de nuevo.");
                }
            }

            var usuario = CargarInterno().FirstOrDefault(u =>
                u.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase));

            bool correcto = usuario != null && PasswordHasher.Verificar(password, usuario.PasswordHash);
            if (!correcto)
            {
                RegistrarFallo(nombreUsuario);
                return null;
            }

            lock (Bloqueos) { Bloqueos.Remove(nombreUsuario); }

            // Solo se revela que la cuenta esta desactivada si la contraseña era correcta
            if (!usuario.Activo)
                throw new ReglaNegocioException("Tu cuenta está desactivada. Pide a un administrador que la active.");

            return usuario;
        }

        private static void RegistrarFallo(string nombreUsuario)
        {
            lock (Bloqueos)
            {
                Bloqueos.TryGetValue(nombreUsuario, out var actual);
                int fallos = actual.Fallos + 1;
                Bloqueos[nombreUsuario] = fallos >= MaxIntentosFallidos
                    ? (0, DateTime.Now.Add(TiempoBloqueo))
                    : (fallos, DateTime.MinValue);
            }
        }

        // ---------------------------------------------------------------
        // Consultas (solo administrador)
        // ---------------------------------------------------------------
        public static List<Usuario> ObtenerTodos()
        {
            SesionService.RequerirAdmin();
            return CargarInterno().OrderBy(u => u.Id).ToList();
        }

        public static bool ExisteUsuario(string usuario, int idExcluir = 0)
        {
            SesionService.RequerirAdmin();
            return Existe(CargarInterno(), usuario, idExcluir);
        }

        private static bool Existe(List<Usuario> lista, string nombre, int idExcluir) =>
            lista.Any(u => u.NombreUsuario.Equals(nombre, StringComparison.OrdinalIgnoreCase) && u.Id != idExcluir);

        // ---------------------------------------------------------------
        // Validaciones
        // ---------------------------------------------------------------
        public static string NormalizarNombre(string nombre) =>
            Regex.Replace((nombre ?? string.Empty).Trim(), @"\s+", " ");

        private static void ValidarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ReglaNegocioException("Captura el nombre de usuario.");
            if (nombre.Length < 3 || nombre.Length > 30)
                throw new ReglaNegocioException("El nombre de usuario debe tener entre 3 y 30 caracteres.");
            if (!Regex.IsMatch(nombre, @"^[\p{L}\p{N}][\p{L}\p{N} ._-]*$"))
                throw new ReglaNegocioException(
                    "El usuario solo puede tener letras, números, espacios, punto, guion y guion bajo.");
        }

        private static void ValidarPassword(string password, string nombreUsuario)
        {
            if (string.IsNullOrEmpty(password))
                throw new ReglaNegocioException("Captura la contraseña.");
            if (password.Any(char.IsWhiteSpace))
                throw new ReglaNegocioException("La contraseña no puede tener espacios.");
            if (password.Length < LongitudMinimaPassword)
                throw new ReglaNegocioException($"La contraseña debe tener al menos {LongitudMinimaPassword} caracteres.");
            if (password.Length > LongitudMaximaPassword)
                throw new ReglaNegocioException($"La contraseña no puede pasar de {LongitudMaximaPassword} caracteres.");
            if (password.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase))
                throw new ReglaNegocioException("La contraseña no puede ser igual al nombre de usuario.");
        }

        private static void ValidarRol(string rol)
        {
            if (!Roles.Todos.Contains(rol))
                throw new ReglaNegocioException("Elige un rol válido.");
        }

        private static int OtrosAdministradoresActivos(List<Usuario> lista, int idExcluir) =>
            lista.Count(u => u.Id != idExcluir && u.EsAdministrador && u.Activo);

        // ---------------------------------------------------------------
        // Administracion (solo administrador)
        // ---------------------------------------------------------------
        public static Usuario Crear(string nombreUsuario, string password, string rol)
        {
            SesionService.RequerirAdmin();

            string nombre = NormalizarNombre(nombreUsuario);
            ValidarNombre(nombre);
            ValidarPassword(password, nombre);
            ValidarRol(rol);

            return JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                if (Existe(lista, nombre, 0))
                    throw new ReglaNegocioException("Ese nombre de usuario ya existe.");

                var nuevo = new Usuario
                {
                    Id = lista.Count == 0 ? 1 : lista.Max(u => u.Id) + 1,
                    NombreUsuario = nombre,
                    PasswordHash = PasswordHasher.Hash(password),
                    Rol = rol,
                    Activo = true,
                    DebeCambiarPassword = true, // el administrador conoce la contraseña inicial: la persona debe cambiarla
                    FechaCreacion = DateTime.Now
                };
                lista.Add(nuevo);
                GuardarInterno(lista);
                return nuevo;
            });
        }

        public static Usuario Actualizar(int id, string nombreUsuario, string rol, bool activo)
        {
            var actor = SesionService.RequerirAdmin();

            string nombre = NormalizarNombre(nombreUsuario);
            ValidarNombre(nombre);
            ValidarRol(rol);

            return JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                var usuario = lista.FirstOrDefault(u => u.Id == id)
                    ?? throw new ReglaNegocioException("El usuario ya no existe.");

                if (Existe(lista, nombre, id))
                    throw new ReglaNegocioException("Ese nombre de usuario ya existe.");

                if (usuario.Id == actor.Id && !activo)
                    throw new ReglaNegocioException("No puedes desactivar tu propia cuenta.");
                if (usuario.Id == actor.Id && rol != Roles.Administrador)
                    throw new ReglaNegocioException("No puedes quitarte tu propio rol de administrador.");

                bool dejaDeSerAdminActivo = usuario.EsAdministrador && usuario.Activo &&
                                            (rol != Roles.Administrador || !activo);
                if (dejaDeSerAdminActivo && OtrosAdministradoresActivos(lista, id) == 0)
                    throw new ReglaNegocioException("Debe existir al menos un administrador activo.");

                usuario.NombreUsuario = nombre;
                usuario.Rol = rol;
                usuario.Activo = activo;
                GuardarInterno(lista);
                return usuario;
            });
        }

        /// <summary>Asigna una contraseña temporal; la persona tendra que cambiarla al iniciar sesion.</summary>
        public static void RestablecerPassword(int id, string passwordTemporal)
        {
            SesionService.RequerirAdmin();

            JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                var usuario = lista.FirstOrDefault(u => u.Id == id)
                    ?? throw new ReglaNegocioException("El usuario ya no existe.");

                ValidarPassword(passwordTemporal, usuario.NombreUsuario);

                usuario.PasswordHash = PasswordHasher.Hash(passwordTemporal);
                usuario.DebeCambiarPassword = true;
                GuardarInterno(lista);
            });

            ReiniciarBloqueos(); // por si la cuenta estaba bloqueada por intentos fallidos
        }

        /// <summary>Borra los contadores de intentos fallidos de login (lo usan las pruebas y el restablecimiento).</summary>
        internal static void ReiniciarBloqueos()
        {
            lock (Bloqueos) { Bloqueos.Clear(); }
        }

        /// <summary>El usuario con sesion iniciada cambia SU contraseña (se usa tras un restablecimiento).</summary>
        public static void EstablecerPasswordPropia(string passwordNueva)
        {
            var actor = SesionService.Requerir();
            ValidarPassword(passwordNueva, actor.NombreUsuario);

            JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                var usuario = lista.First(u => u.Id == actor.Id);
                usuario.PasswordHash = PasswordHasher.Hash(passwordNueva);
                usuario.DebeCambiarPassword = false;
                GuardarInterno(lista);
            });
        }

        /// <summary>
        /// Elimina un usuario SOLO si nunca registro pedidos ni cortes.
        /// Si ya tiene historial no se borra (se desactiva) para no perder informacion de ventas.
        /// </summary>
        public static void Eliminar(int id)
        {
            var actor = SesionService.RequerirAdmin();

            JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                var usuario = lista.FirstOrDefault(u => u.Id == id)
                    ?? throw new ReglaNegocioException("El usuario ya no existe.");

                if (usuario.Id == actor.Id)
                    throw new ReglaNegocioException("No puedes eliminar tu propia cuenta.");
                if (usuario.EsAdministrador && usuario.Activo && OtrosAdministradoresActivos(lista, id) == 0)
                    throw new ReglaNegocioException("Debe existir al menos un administrador activo.");
                if (PedidoService.UsuarioTieneHistorial(id) || CorteService.UsuarioTieneCortes(id))
                    throw new ReglaNegocioException(
                        $"\"{usuario.NombreUsuario}\" ya tiene pedidos o cortes registrados. " +
                        "Para conservar el historial de ventas no se puede eliminar; desactívalo en su lugar.");

                lista.Remove(usuario);
                GuardarInterno(lista);
            });
        }
    }
}
