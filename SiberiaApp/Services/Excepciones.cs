using System;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Una regla del negocio o de seguridad no se cumple (permiso, validacion, dato repetido...).
    /// El mensaje esta escrito para mostrarse tal cual al usuario en un InfoBar.
    /// </summary>
    public class ReglaNegocioException : Exception
    {
        public ReglaNegocioException(string mensaje) : base(mensaje) { }
    }

    /// <summary>El precio del menu cambio mientras se capturaba el pedido.</summary>
    public class PrecioCambiadoException : ReglaNegocioException
    {
        public PrecioCambiadoException(string mensaje) : base(mensaje) { }
    }

    /// <summary>Un archivo .json esta dañado y no se pudo recuperar. Se conserva sin tocar.</summary>
    public class DatosCorruptosException : Exception
    {
        public DatosCorruptosException(string mensaje) : base(mensaje) { }
    }
}
