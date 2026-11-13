using System;
using Newtonsoft.Json;

namespace SiberiaApp.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;

        /// <summary>
        /// SOLO para leer usuarios.json de versiones anteriores (contraseña en texto plano).
        /// Al iniciar la app se convierte a <see cref="PasswordHash"/> y este campo se borra;
        /// despues ya no se vuelve a escribir en el archivo.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Password { get; set; }

        /// <summary>Contraseña protegida (PBKDF2 + sal). Nunca se guarda la contraseña real.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        public string Rol { get; set; } = Roles.Cajero; // Administrador / Cajero

        /// <summary>Una cuenta desactivada no puede iniciar sesion, pero conserva su historial.</summary>
        public bool Activo { get; set; } = true;

        /// <summary>Si es true, al iniciar sesion se obliga a elegir una contraseña nueva.</summary>
        public bool DebeCambiarPassword { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool EsAdministrador => Rol == Roles.Administrador;
    }
}
