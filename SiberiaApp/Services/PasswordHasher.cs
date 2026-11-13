using System;
using System.Security.Cryptography;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Protege las contraseñas con PBKDF2 (viene incluido en .NET, no usa paquetes extra).
    /// Formato guardado:  PBKDF2$iteraciones$sal$hash   (sal y hash en Base64)
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iteraciones = 100_000;
        private const int TamanoSal = 16;
        private const int TamanoHash = 32;

        public static string Hash(string password)
        {
            byte[] sal = RandomNumberGenerator.GetBytes(TamanoSal);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
            return $"PBKDF2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verificar(string password, string almacenado)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(almacenado))
                return false;

            string[] partes = almacenado.Split('$');
            if (partes.Length != 4 || partes[0] != "PBKDF2") return false;
            if (!int.TryParse(partes[1], out int iteraciones) || iteraciones <= 0) return false;

            try
            {
                byte[] sal = Convert.FromBase64String(partes[2]);
                byte[] esperado = Convert.FromBase64String(partes[3]);
                byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(password, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
                return CryptographicOperations.FixedTimeEquals(calculado, esperado);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
