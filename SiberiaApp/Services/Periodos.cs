using System;

namespace SiberiaApp.Services
{
    public enum TipoPeriodo { Dia, Semana, Mes, Personalizado }

    /// <summary>
    /// Reglas de fechas para reportes y cortes, en UN solo lugar.
    /// La semana va de domingo a sabado (igual que el reporte semanal que ya tenia la app).
    /// </summary>
    public static class Periodos
    {
        public static DateTime InicioSemana(DateTime fecha) =>
            fecha.Date.AddDays(-(int)fecha.DayOfWeek);

        public static DateTime InicioMes(DateTime fecha) =>
            new DateTime(fecha.Year, fecha.Month, 1);

        public static (DateTime Desde, DateTime Hasta) Rango(
            TipoPeriodo tipo, DateTime referencia, DateTime? desde = null, DateTime? hasta = null)
        {
            referencia = referencia.Date;
            switch (tipo)
            {
                case TipoPeriodo.Dia:
                    return (referencia, referencia);
                case TipoPeriodo.Semana:
                    var inicio = InicioSemana(referencia);
                    return (inicio, inicio.AddDays(6));
                case TipoPeriodo.Mes:
                    var primero = InicioMes(referencia);
                    return (primero, primero.AddMonths(1).AddDays(-1));
                default:
                    var d = (desde ?? referencia).Date;
                    var h = (hasta ?? referencia).Date;
                    if (h < d)
                        throw new ReglaNegocioException("La fecha final no puede ser anterior a la inicial.");
                    return (d, h);
            }
        }
    }
}
