using System;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace SiberiaApp.Helpers
{
    /// <summary>
    /// Reglas de captura que antes estaban copiadas en cada pantalla:
    ///  - texto: no permite espacios al inicio ni dos espacios seguidos (tambien limpia lo que se pegue)
    ///  - contraseña: no permite ningun espacio
    /// Uso:  CamposTexto.SinEspaciosRepetidos(txtCliente);
    /// </summary>
    public static class CamposTexto
    {
        public static void SinEspaciosRepetidos(TextBox caja)
        {
            caja.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != VirtualKey.Space || s is not TextBox tb) return;

                // espacio al inicio, o justo despues de otro espacio
                if (tb.SelectionStart == 0 ||
                    (tb.SelectionStart > 0 && tb.Text[tb.SelectionStart - 1] == ' '))
                {
                    e.Handled = true;
                }
            };

            caja.TextChanged += (s, e) =>
            {
                if (s is not TextBox tb) return;

                int cursor = tb.SelectionStart;
                string original = tb.Text;
                string limpio = Regex.Replace(original.TrimStart(), @"\s+", " ");
                if (original != limpio)
                {
                    tb.Text = limpio;
                    tb.SelectionStart = Math.Min(cursor, tb.Text.Length);
                }
            };
        }

        public static void SinEspacios(PasswordBox caja)
        {
            caja.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Space)
                    e.Handled = true;
            };

            caja.PasswordChanged += (s, e) =>
            {
                if (s is PasswordBox pb && pb.Password.Contains(' '))
                    pb.Password = pb.Password.Replace(" ", string.Empty);
            };
        }
    }
}
