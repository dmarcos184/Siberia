namespace SiberiaApp.Models
{
    /// <summary>Estados posibles de un pedido (los mismos textos que ya guardaba pedidos.json).</summary>
    public static class EstadosPedido
    {
        public const string Pendiente = "Pendiente";
        public const string EnPreparacion = "En preparacion";
        public const string Entregado = "Entregado";
        public const string Cancelado = "Cancelado";

        public static readonly string[] Todos = { Pendiente, EnPreparacion, Entregado, Cancelado };

        /// <summary>Un pedido "finalizado" ya no se puede modificar sin autorizacion de un administrador.</summary>
        public static bool EsFinal(string estado) => estado == Entregado || estado == Cancelado;
    }
}

namespace SiberiaApp.Models
{
    /// <summary>Tipos de pedido que existen.</summary>
    public static class TiposPedido
    {
        public const string Mostrador = "Mostrador";
        public const string Domicilio = "Domicilio";

        public static readonly string[] Todos = { Mostrador, Domicilio };
    }
}
