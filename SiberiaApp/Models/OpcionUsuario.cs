namespace SiberiaApp.Models
{
    /// <summary>Una opcion de los filtros "Usuario" de las pantallas (Id null = todos).</summary>
    public class OpcionUsuario
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public override string ToString() => Nombre;
    }
}
