using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SiberiaApp.Models;
using SiberiaApp.Services;
using Windows.System;

namespace SiberiaApp.Helpers
{
    /// <summary>
    /// Ventana para agregar, ver y quitar las notas de UN producto del pedido.
    /// Ejemplo para "4 Tostada Especial":   1 sin crema   ·   2 con extra de aguacate
    /// Las reglas (maximo de notas, largo, unidades validas) las valida PedidoService.ValidarNotas,
    /// la misma funcion que se usa al guardar el pedido.
    /// </summary>
    public static class NotasDialog
    {
        // Atajos con las indicaciones mas comunes del negocio (un toque y se llena el texto)
        private static readonly string[] Atajos =
        {
            "sin crema", "sin cebolla", "sin salsa", "sin tomate",
            "extra de crema", "extra de aguacate", "para llevar"
        };

        /// <summary>true si la persona guardo cambios (detalle.Notas ya queda actualizado).</summary>
        public static async Task<bool> MostrarAsync(XamlRoot xamlRoot, DetallePedido detalle)
        {
            // Se trabaja sobre una copia: si cancelan, el pedido no cambia
            var notas = (detalle.Notas ?? new List<NotaProducto>()).Select(n => n.Copiar()).ToList();

            var listaNotas = new StackPanel { Spacing = 4 };
            var aviso = new InfoBar { Severity = InfoBarSeverity.Warning, IsClosable = true, IsOpen = false };

            var numUnidades = new NumberBox
            {
                Header = "Unidades",
                Minimum = 1,
                Maximum = Math.Max(1, detalle.Cantidad),
                Value = 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width = 130
            };
            var txtNota = new TextBox
            {
                Header = "Indicación",
                PlaceholderText = "Ej. sin crema",
                MaxLength = PedidoService.LongitudMaximaNota,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            CamposTexto.SinEspaciosRepetidos(txtNota);

            void Redibujar()
            {
                listaNotas.Children.Clear();
                if (notas.Count == 0)
                {
                    listaNotas.Children.Add(new TextBlock
                    {
                        Text = "Sin notas todavía.",
                        Opacity = 0.7
                    });
                    return;
                }

                foreach (var nota in notas)
                {
                    var fila = new Grid { ColumnSpacing = 8 };
                    fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var texto = new TextBlock
                    {
                        Text = $"{nota.Cantidad} {nota.Texto}",
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    var quitar = new Button { Content = new SymbolIcon(Symbol.Delete) };
                    ToolTipService.SetToolTip(quitar, "Quitar esta nota");
                    var notaActual = nota;
                    quitar.Click += (s, e) =>
                    {
                        notas.Remove(notaActual);
                        aviso.IsOpen = false;
                        Redibujar();
                    };

                    Grid.SetColumn(quitar, 1);
                    fila.Children.Add(texto);
                    fila.Children.Add(quitar);
                    listaNotas.Children.Add(fila);
                }
            }

            // Agrega la nota escrita a la lista. false = no se pudo (se muestra el motivo).
            bool Agregar()
            {
                string texto = txtNota.Text.Trim();
                if (texto.Length == 0)
                {
                    aviso.Message = "Escribe la indicación (por ejemplo: sin crema).";
                    aviso.IsOpen = true;
                    return false;
                }

                int unidades = double.IsNaN(numUnidades.Value) ? 1 : (int)numUnidades.Value;
                var prueba = notas.Select(n => n.Copiar()).ToList();
                prueba.Add(new NotaProducto { Cantidad = unidades, Texto = texto });

                try
                {
                    // misma validacion que al guardar el pedido
                    var validas = PedidoService.ValidarNotas(
                        new DetallePedido { Cantidad = detalle.Cantidad, Notas = prueba }, detalle.NombreProducto);
                    notas.Clear();
                    notas.AddRange(validas);
                }
                catch (ReglaNegocioException ex)
                {
                    aviso.Message = ex.Message;
                    aviso.IsOpen = true;
                    return false;
                }

                txtNota.Text = string.Empty;
                numUnidades.Value = 1;
                aviso.IsOpen = false;
                Redibujar();
                return true;
            }

            // ---- Atajos (3 por renglon) ----
            var atajos = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
            for (int c = 0; c < 3; c++)
                atajos.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < Atajos.Length; i++)
            {
                if (i % 3 == 0)
                    atajos.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                string sugerencia = Atajos[i];
                var boton = new Button { Content = sugerencia, HorizontalAlignment = HorizontalAlignment.Stretch };
                boton.Click += (s, e) =>
                {
                    txtNota.Text = sugerencia;
                    txtNota.Focus(FocusState.Programmatic);
                    txtNota.SelectionStart = txtNota.Text.Length;
                };
                Grid.SetRow(boton, i / 3);
                Grid.SetColumn(boton, i % 3);
                atajos.Children.Add(boton);
            }

            // ---- Fila para escribir la nota ----
            var botonAgregar = new Button { Content = "Agregar nota", VerticalAlignment = VerticalAlignment.Bottom };
            botonAgregar.Click += (s, e) => Agregar();
            txtNota.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    Agregar();
                    e.Handled = true;
                }
            };

            var filaNueva = new Grid { ColumnSpacing = 8 };
            filaNueva.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filaNueva.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filaNueva.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(txtNota, 1);
            Grid.SetColumn(botonAgregar, 2);
            filaNueva.Children.Add(numUnidades);
            filaNueva.Children.Add(txtNota);
            filaNueva.Children.Add(botonAgregar);

            var contenido = new StackPanel { Spacing = 12, MinWidth = 460 };
            contenido.Children.Add(new TextBlock
            {
                Text = $"Pedido: {detalle.Cantidad} × {detalle.NombreProducto}. " +
                       "Indica a cuántas unidades aplica cada nota (ej. 1 sin crema).",
                TextWrapping = TextWrapping.Wrap
            });
            contenido.Children.Add(new ScrollViewer { MaxHeight = 150, Content = listaNotas });
            contenido.Children.Add(filaNueva);
            contenido.Children.Add(atajos);
            contenido.Children.Add(aviso);

            Redibujar();

            var dialogo = new ContentDialog
            {
                Title = "Notas del producto",
                Content = contenido,
                PrimaryButtonText = "Guardar notas",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
            };

            // Si dejaron una nota escrita sin pulsar "Agregar", se agrega al guardar (o avisa si es invalida)
            dialogo.PrimaryButtonClick += (s, args) =>
            {
                if (!string.IsNullOrWhiteSpace(txtNota.Text) && !Agregar())
                    args.Cancel = true;
            };

            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
                return false;

            detalle.Notas = notas;
            return true;
        }
    }
}
