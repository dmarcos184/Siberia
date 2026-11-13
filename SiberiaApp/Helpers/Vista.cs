using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SiberiaApp.Models;

namespace SiberiaApp.Helpers
{
    /// <summary>Pequeñas funciones para usar con x:Bind en los XAML (visibilidad y colores).</summary>
    public static class Vista
    {
        public static Visibility Visible(bool valor) => valor ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility VisibleSiTexto(string texto) =>
            string.IsNullOrWhiteSpace(texto) ? Visibility.Collapsed : Visibility.Visible;

        private static SolidColorBrush _verde, _rojo, _azul, _ambar;

        private static SolidColorBrush Crear(byte r, byte g, byte b) =>
            new SolidColorBrush(ColorHelper.FromArgb(255, r, g, b));

        /// <summary>Color del texto de cada estado de pedido (se lee bien en tema claro y oscuro).</summary>
        public static Brush ColorEstado(string estado)
        {
            switch (estado)
            {
                case EstadosPedido.Entregado: return _verde ??= Crear(0x2E, 0xA0, 0x4F);
                case EstadosPedido.Cancelado: return _rojo ??= Crear(0xE0, 0x4B, 0x4B);
                case EstadosPedido.EnPreparacion: return _azul ??= Crear(0x3B, 0x8E, 0xEA);
                default: return _ambar ??= Crear(0xD9, 0x8E, 0x04);
            }
        }

        /// <summary>Verde si esta activo, rojo si no (para listas de usuarios y productos).</summary>
        public static Brush ColorActivo(bool activo) =>
            activo ? ColorEstado(EstadosPedido.Entregado) : ColorEstado(EstadosPedido.Cancelado);

        /// <summary>Verde si el corte esta cerrado, ambar si esta abierto.</summary>
        public static Brush ColorCorte(string estado) =>
            estado == Corte.Cerrado ? ColorEstado(EstadosPedido.Entregado) : ColorEstado(EstadosPedido.Pendiente);
    }
}
