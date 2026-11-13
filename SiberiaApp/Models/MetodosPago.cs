namespace SiberiaApp.Models
{
    /// <summary>
    /// Formas de pago. Los pedidos guardados antes de esta version no traen metodo:
    /// quedan como "Sin registrar" (no se les inventa uno).
    /// </summary>
    public static class MetodosPago
    {
        public const string Efectivo = "Efectivo";
        public const string Tarjeta = "Tarjeta";
        public const string Transferencia = "Transferencia";
        public const string SinRegistrar = "Sin registrar";

        /// <summary>Los que se pueden elegir al tomar un pedido.</summary>
        public static readonly string[] Todos = { Efectivo, Tarjeta, Transferencia };

        public static string Nombre(string metodo) =>
            string.IsNullOrWhiteSpace(metodo) ? SinRegistrar : metodo;
    }
}
