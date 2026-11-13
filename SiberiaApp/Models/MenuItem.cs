namespace SiberiaApp.Models
{
    public class MenuItem
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }

        /// <summary>
        /// Costo de preparar una unidad. Es OPCIONAL: si no se captura (null) las ganancias
        /// de ese producto NO se calculan (el sistema nunca inventa costos).
        /// </summary>
        public decimal? Costo { get; set; }

        public bool Activo { get; set; } = true;

        public override string ToString() => $"{Nombre} - ${Precio:F2}";
    }
}
