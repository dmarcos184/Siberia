using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SiberiaApp.Models;

namespace SiberiaApp.Services
{
    public static class MenuService
    {
        private const string Archivo = "menu.json";

        public const int LongitudMaximaNombre = 50;
        public const int LongitudMaximaCategoria = 30;
        public const decimal PrecioMaximo = 100000m;

        internal static List<MenuItem> CargarInterno()
        {
            var lista = JsonDataService.Cargar<MenuItem>(Archivo);
            if (lista.Count > 0) return lista;

            return JsonDataService.Transaccion(() =>
            {
                var actual = JsonDataService.Cargar<MenuItem>(Archivo);
                if (actual.Count > 0) return actual;
                actual = ObtenerMenuPorDefecto();
                JsonDataService.Guardar(Archivo, actual);
                return actual;
            });
        }

        // Cualquier usuario con sesion puede ver el menu (lo necesita para tomar pedidos)
        public static List<MenuItem> ObtenerTodos()
        {
            SesionService.Requerir();
            return CargarInterno();
        }

        public static List<MenuItem> ObtenerActivos() =>
            ObtenerTodos().Where(m => m.Activo).ToList();

        // ---------------------------------------------------------------
        // Validacion
        // ---------------------------------------------------------------
        private static void Validar(MenuItem item, List<MenuItem> lista)
        {
            item.Nombre = Regex.Replace((item.Nombre ?? string.Empty).Trim(), @"\s+", " ");
            item.Categoria = Regex.Replace((item.Categoria ?? string.Empty).Trim(), @"\s+", " ");

            if (item.Nombre.Length == 0)
                throw new ReglaNegocioException("Escribe el nombre del producto.");
            if (item.Nombre.Length > LongitudMaximaNombre)
                throw new ReglaNegocioException($"El nombre no puede pasar de {LongitudMaximaNombre} caracteres.");
            if (item.Categoria.Length == 0)
                throw new ReglaNegocioException("Escribe o selecciona una categoría.");
            if (item.Categoria.Length > LongitudMaximaCategoria)
                throw new ReglaNegocioException($"La categoría no puede pasar de {LongitudMaximaCategoria} caracteres.");

            if (item.Precio <= 0)
                throw new ReglaNegocioException("El precio debe ser mayor a cero.");
            if (item.Precio > PrecioMaximo)
                throw new ReglaNegocioException($"El precio no puede ser mayor a {PrecioMaximo:C0}.");
            item.Precio = Math.Round(item.Precio, 2);

            if (item.Costo.HasValue)
            {
                if (item.Costo.Value < 0)
                    throw new ReglaNegocioException("El costo no puede ser negativo.");
                if (item.Costo.Value > PrecioMaximo)
                    throw new ReglaNegocioException($"El costo no puede ser mayor a {PrecioMaximo:C0}.");
                item.Costo = Math.Round(item.Costo.Value, 2);
            }

            if (lista.Any(m => m.Id != item.Id &&
                               m.Nombre.Equals(item.Nombre, StringComparison.OrdinalIgnoreCase)))
                throw new ReglaNegocioException("Ya existe un producto con ese nombre.");
        }

        // ---------------------------------------------------------------
        // Administracion (solo administrador)
        // ---------------------------------------------------------------
        public static void Agregar(MenuItem item)
        {
            SesionService.RequerirAdmin();
            JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                item.Id = 0;
                Validar(item, lista);
                item.Id = lista.Count == 0 ? 1 : lista.Max(m => m.Id) + 1;
                lista.Add(item);
                JsonDataService.Guardar(Archivo, lista);
            });
        }

        public static void Actualizar(MenuItem item)
        {
            SesionService.RequerirAdmin();
            JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                int idx = lista.FindIndex(m => m.Id == item.Id);
                if (idx < 0)
                    throw new ReglaNegocioException("El producto ya no existe.");

                Validar(item, lista);
                lista[idx] = item;
                JsonDataService.Guardar(Archivo, lista);
            });
        }

        /// <summary>
        /// Solo se puede eliminar un producto que nunca se ha vendido.
        /// Si ya aparece en pedidos, hay que desactivarlo (asi el historial de ventas queda completo).
        /// </summary>
        public static void Eliminar(int id)
        {
            SesionService.RequerirAdmin();
            JsonDataService.Transaccion(() =>
            {
                var lista = CargarInterno();
                var item = lista.FirstOrDefault(m => m.Id == id)
                    ?? throw new ReglaNegocioException("El producto ya no existe.");

                if (PedidoService.ProductoTieneVentas(id))
                    throw new ReglaNegocioException(
                        $"\"{item.Nombre}\" ya aparece en pedidos. Para conservar el historial no se elimina; " +
                        "desactívalo y dejará de salir al tomar pedidos.");

                lista.Remove(item);
                JsonDataService.Guardar(Archivo, lista);
            });
        }

        private static List<MenuItem> ObtenerMenuPorDefecto()
        {
            return new List<MenuItem>
            {
                    new MenuItem{ Id = 1, Nombre = "Tostada Especial",      Categoria = "Tostada",    Precio = 85 },
                    new MenuItem{ Id = 2, Nombre = "Tostada Sencilla",    Categoria = "Tostada",    Precio = 75 },
                    new MenuItem{ Id = 3, Nombre = "Tostada Especial Junior",     Categoria = "Tostada Jr",    Precio = 60 },
                    new MenuItem{ Id = 4, Nombre = "Tostada Sencilla Junior",   Categoria = "Tostada Jr", Precio = 57 },
                    new MenuItem{ Id = 5, Nombre = "Taco Especial",  Categoria = "Taco", Precio = 75 },
                    new MenuItem{ Id = 6, Nombre = "Taco Sencillo",    Categoria = "Taco",  Precio = 65 },
                    new MenuItem{ Id = 7, Nombre = "Taco Especial Junior",   Categoria = "Taco Jr",  Precio = 50 },
                    new MenuItem{ Id = 8, Nombre = "Taco Sencillo Junior",    Categoria = "Taco Jr",    Precio = 47 },
                    new MenuItem{ Id = 10, Nombre = "Ensalada Pollo", Categoria = "Ensalada", Precio = 125 }

            };
        }
    }
}
