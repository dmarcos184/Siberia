using Newtonsoft.Json;

namespace SiberiaApp.Models
{
    /// <summary>
    /// Una indicacion para una parte de un producto del pedido.
    /// Ejemplo: en "4 tostadas", la nota { Cantidad = 1, Texto = "sin crema" }
    /// significa "1 sin crema".
    /// </summary>
    public class NotaProducto
    {
        /// <summary>A cuantas unidades del producto aplica la nota (1 hasta la cantidad del producto).</summary>
        public int Cantidad { get; set; } = 1;

        public string Texto { get; set; } = string.Empty;

        [JsonIgnore]
        public string Descripcion => $"{Cantidad} {Texto}";

        public NotaProducto Copiar() => new NotaProducto { Cantidad = Cantidad, Texto = Texto };
    }
}
