using System;

namespace SiberiaApp.Helpers
{
    /// <summary>
    /// Funciones para mostrar valores como texto en las tablas (ListView).
    /// Se usan desde el XAML asi:  Text="{x:Bind helpers:Formato.Dinero(Precio)}"
    /// </summary>
    public static class Formato
    {
        public static string Dinero(decimal valor) => valor.ToString("C2");

        /// <summary>Dinero, o "Sin capturar" cuando el dato no existe (ej. costo sin capturar).</summary>
        public static string DineroONulo(decimal? valor) => valor.HasValue ? valor.Value.ToString("C2") : "Sin capturar";

        public static string SiNo(bool valor) => valor ? "Sí" : "No";

        public static string ActivoTexto(bool valor) => valor ? "Activo" : "Desactivado";

        public static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy");

        public static string FechaHora(DateTime? fecha) => fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty;

        /// <summary>Variacion entre periodos, ej. "▲ 12.5 %" o "▼ 3.0 %". null = no hay periodo anterior con ventas.</summary>
        public static string Variacion(decimal? porcentaje)
        {
            if (!porcentaje.HasValue) return "Sin datos del periodo anterior";
            decimal v = Math.Round(porcentaje.Value, 1);
            if (v > 0) return $"▲ {v:0.0} % vs. periodo anterior";
            if (v < 0) return $"▼ {Math.Abs(v):0.0} % vs. periodo anterior";
            return "= igual que el periodo anterior";
        }
    }
}
