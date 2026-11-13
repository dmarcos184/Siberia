# Siberia - Sistema de Pedidos (C# WinUI 3)

Aplicación de escritorio en C# (.NET 8 / **WinUI 3**, Windows App SDK 1.6) para
tomar pedidos, con persistencia en archivos **JSON** usando **Newtonsoft.Json**.

La interfaz antes era WinForms; ahora usa WinUI 3 con estilo Windows 11
(menú lateral, fondo Mica, tarjetas). La lógica (Models y Services) es la misma.

## Cómo abrirlo

1. **Visual Studio 2022** con la carga de trabajo **"Desarrollo de aplicaciones
   WinUI"** (en el instalador: *Desarrollo de escritorio de .NET* → marcar
   *Herramientas de la Aplicación para Windows App SDK / WinUI*).
2. Abre `SiberiaApp.sln` (un nivel arriba de esta carpeta).
3. Arriba en la barra, la plataforma debe decir **x64** (no "Any CPU").
4. Presiona **F5**. NuGet restaura solo `Microsoft.WindowsAppSDK` y `Newtonsoft.Json`.

## Usuario por defecto

- **Usuario:** admin
- **Contraseña inicial:** admin123 (solo en una instalación nueva; el sistema te obliga a cambiarla al entrar)

Si ya tenías datos de la versión anterior, tus usuarios, menú y pedidos se conservan: las contraseñas
se convierten a hash automáticamente al abrir la app (ver `CAMBIOS_MEJORA.txt`).

## Estructura del proyecto

```
SiberiaApp/
├── SiberiaApp.csproj
├── App.xaml / App.xaml.cs          (arranque + ESTILOS de toda la app)
├── MainWindow.xaml / .cs           (la ventana, barra de título, Mica)
├── Views/                          (cada pestaña ahora es una página)
│   ├── LoginPage                   (antes LoginForm)
│   ├── ShellPage                   (menú lateral, antes MainForm/TabControl)
│   ├── NuevaOrdenPage              (1. Nueva orden)
│   ├── MenuPage                    (2. Menú y precios)
│   ├── ConsultarPedidosPage        (3. Consultar pedidos)
│   ├── ActualizarPedidoPage        (4. Actualizar pedido)
│   ├── ReportesPage                (5. Reportes)
│   └── UsuariosPage                (6. Usuarios)
├── Helpers/                        (Avisos = reemplazo de MessageBox, Formato)
├── Models/                         (sin cambios + FilaPedido)
├── Services/                       (sin cambios)
└── Data/                           (se genera junto al .exe al ejecutar)
```

Lee `CAMBIOS.txt` para la explicación completa de qué cambió y cómo editar
el diseño con WinUI Gallery.
