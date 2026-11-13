using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace SiberiaApp.Services
{
    /// <summary>
    /// Se conecta a archivos JSON (nuestra "base de datos") para guardar y
    /// leer cualquier lista de objetos usando el paquete NuGet Newtonsoft.Json.
    ///
    /// Mejoras de integridad:
    ///  - Transaccion(): leer-modificar-guardar se hace de uno en uno (candado dentro de la app
    ///    + Mutex para dos ventanas/instancias abiertas), asi no se pisan los datos.
    ///  - Guardar() escribe a un archivo temporal y lo reemplaza (si se va la luz a media escritura
    ///    el archivo bueno no se corrompe) y deja una copia .bak de la version anterior.
    ///  - Cargar() YA NO devuelve una lista vacia cuando el archivo esta dañado (eso hacia que el
    ///    siguiente guardado borrara todo): intenta el .bak y si no puede, avisa con una excepcion.
    /// </summary>
    public static class JsonDataService
    {
        private static readonly object Candado = new object();
        private static string _carpetaDatos =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

        /// <summary>Carpeta de los .json. Se puede cambiar (las pruebas usan una carpeta temporal).</summary>
        public static string CarpetaDatos
        {
            get => _carpetaDatos;
            set
            {
                _carpetaDatos = value;
                Directory.CreateDirectory(_carpetaDatos);
            }
        }

        static JsonDataService()
        {
            Directory.CreateDirectory(_carpetaDatos);
        }

        private static string RutaArchivo(string nombreArchivo) =>
            Path.Combine(CarpetaDatos, nombreArchivo);

        // ---------------------------------------------------------------
        // Transacciones
        // ---------------------------------------------------------------
        private static string NombreMutex()
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(CarpetaDatos).ToLowerInvariant()));
            return "SiberiaApp_Datos_" + Convert.ToHexString(hash, 0, 8);
        }

        /// <summary>Ejecuta una operacion completa de lectura + escritura sin que nadie mas la interrumpa.</summary>
        public static T Transaccion<T>(Func<T> accion)
        {
            lock (Candado)
            {
                using var mutex = new Mutex(false, NombreMutex());
                bool adquirido = false;
                try
                {
                    try { adquirido = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
                    catch (AbandonedMutexException) { adquirido = true; }

                    if (!adquirido)
                        throw new ReglaNegocioException(
                            "Los datos están siendo usados por otra operación. Intenta de nuevo en unos segundos.");

                    return accion();
                }
                finally
                {
                    if (adquirido) mutex.ReleaseMutex();
                }
            }
        }

        public static void Transaccion(Action accion) =>
            Transaccion<bool>(() => { accion(); return true; });

        // ---------------------------------------------------------------
        // Leer
        // ---------------------------------------------------------------
        public static List<T> Cargar<T>(string nombreArchivo)
        {
            string ruta = RutaArchivo(nombreArchivo);
            if (!File.Exists(ruta))
                return new List<T>();

            string json = File.ReadAllText(ruta);
            if (string.IsNullOrWhiteSpace(json))
                return File.Exists(ruta + ".bak") ? Recuperar<T>(ruta) : new List<T>();

            try
            {
                return Deserializar<T>(json);
            }
            catch (JsonException)
            {
                return Recuperar<T>(ruta);
            }
        }

        private static List<T> Deserializar<T>(string json) =>
            JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();

        private static List<T> Recuperar<T>(string ruta)
        {
            string respaldo = ruta + ".bak";
            if (File.Exists(respaldo))
            {
                try
                {
                    var datos = Deserializar<T>(File.ReadAllText(respaldo));
                    // Se conserva el archivo dañado para poder revisarlo y se restaura la copia anterior.
                    File.Copy(ruta, ruta + ".corrupto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true);
                    File.Copy(respaldo, ruta, true);
                    return datos;
                }
                catch (JsonException)
                {
                    // el respaldo tampoco sirve: se cae al error de abajo
                }
            }

            throw new DatosCorruptosException(
                $"El archivo \"{Path.GetFileName(ruta)}\" está dañado y no se pudo recuperar. " +
                "No se modificó nada. Revisa la carpeta Data o restaura una copia de seguridad.");
        }

        // ---------------------------------------------------------------
        // Guardar (atomico)
        // ---------------------------------------------------------------
        public static void Guardar<T>(string nombreArchivo, List<T> datos)
        {
            string ruta = RutaArchivo(nombreArchivo);
            string json = JsonConvert.SerializeObject(datos, Formatting.Indented);

            string temporal = ruta + ".tmp";
            File.WriteAllText(temporal, json);

            if (File.Exists(ruta))
                File.Replace(temporal, ruta, ruta + ".bak"); // deja la version anterior como .bak
            else
                File.Move(temporal, ruta);
        }
    }
}
