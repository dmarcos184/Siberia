using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Quien inicio sesion. TODAS las operaciones protegidas de los servicios llaman a
    /// Requerir() / RequerirAdmin(), que vuelven a leer al usuario del archivo para comprobar
    /// que sigue existiendo, sigue activo y tiene el rol que dice tener. Asi no basta con
    /// ocultar botones en la pantalla.
    /// </summary>
    public static class SesionService
    {
        public static Usuario Actual { get; private set; }

        public static bool EsAdmin => Actual != null && Actual.EsAdministrador;

        public static void Iniciar(Usuario usuario) => Actual = usuario;

        public static void Cerrar() => Actual = null;

        /// <summary>Usuario autenticado y vigente; si no, lanza ReglaNegocioException.</summary>
        public static Usuario Requerir()
        {
            var sesion = Actual;
            if (sesion == null)
                throw new ReglaNegocioException("Debes iniciar sesión para realizar esta operación.");

            var vigente = UsuarioService.ObtenerPorIdInterno(sesion.Id);
            if (vigente == null || !vigente.Activo)
            {
                Actual = null;
                throw new ReglaNegocioException("Tu cuenta ya no está activa. Inicia sesión de nuevo.");
            }

            Actual = vigente; // refresca el rol por si un administrador lo cambio
            return vigente;
        }

        public static Usuario RequerirAdmin()
        {
            var usuario = Requerir();
            if (!usuario.EsAdministrador)
                throw new ReglaNegocioException("Esta operación solo la puede realizar un administrador.");
            return usuario;
        }
    }
}
