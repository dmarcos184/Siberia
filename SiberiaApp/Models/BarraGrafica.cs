namespace SiberiaApp.Models
{
    /// <summary>Una barra de la grafica de ventas del panel (solo para la pantalla).</summary>
    public class BarraGrafica
    {
        public double Altura { get; set; }
        public string Valor { get; set; } = string.Empty;
        public string Etiqueta { get; set; } = string.Empty;
    }
}
