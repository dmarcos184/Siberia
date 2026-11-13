namespace SiberiaApp.Models
{
    /// <summary>Roles que existen en el sistema (se guardan como texto en usuarios.json).</summary>
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string Cajero = "Cajero"; // "usuario normal"

        public static readonly string[] Todos = { Cajero, Administrador };
    }
}
