# EarClarinet

A per-app volume mixer for Windows. Press a hotkey and a compact mixer appears
at any edge of your screen: adjust each app's volume independently, mute
anything with one click, and see which app is playing audio.

## Features

- Per-app volume and mute via the Windows Core Audio API (no extra services)
- Overlay in **8 positions**: left/right vertical column, or a horizontal dock
  at top/bottom left, center or right
- System volume row with live peak meter
- TAB cycle between apps with visual hints (TAB / M chips, optional)
- Configurable global hotkey (default `Ctrl+Alt+M`)
- Tray icon, launch on startup, dark/light theme, accent color presets
- Multi-monitor aware, DPI aware (PerMonitorV2)
- Sessions are grouped by process name and ordered by last activity

## Usage

1. Run EarClarinet (tray icon appears).
2. Press `Ctrl+Alt+M` to show/hide the mixer on the monitor where the cursor is.
3. Hover a bar and use the **mouse wheel**, **drag horizontally**, or the keys.

| Key           | Action                  |
| ------------- | ----------------------- |
| `Ctrl+Alt+M`  | Toggle the mixer        |
| `↑` / `↓`     | Cycle focus             |
| `←` / `→`     | Volume − / +            |
| `PageUp/Down` | Fine volume step        |
| `M` / `Space` | Mute the focused app    |
| `Tab`         | Cycle focus (alternate) |
| `Esc`         | Close the mixer         |

## Install

Download the latest installer from
[Releases](https://github.com/gatacampestre/EarClarinet/releases) (or run the
portable exe). The build is self-contained — no .NET runtime needed.

## Building

```powershell
dotnet build EarClarinet.slnx -c Release
dotnet test tests/EarClarinet.Tests/EarClarinet.Tests.csproj -c Release
```

Requires the .NET 10 SDK on Windows 10/11.

## License

MIT — see [LICENSE](LICENSE).

If EarClarinet is useful to you, consider supporting its development:
[ko-fi.com/gatacampestre](https://ko-fi.com/gatacampestre)

---

# Español

Un mezclador de volumen por aplicación para Windows. Pulsa un atajo y aparece
un mezclador compacto en cualquier borde de la pantalla: ajusta el volumen de
cada aplicación de forma independiente, silencia cualquier cosa con un clic y
ve qué aplicación está reproduciendo audio.

## Funciones

- Volumen y silencio por aplicación vía Windows Core Audio API
- Overlay en **8 posiciones**: columna vertical izquierda/derecha, o dock
  horizontal arriba/abajo en izquierda, centro o derecha
- Fila de volumen del sistema con medidor de pico en vivo
- Ciclo con Tab entre aplicaciones con pistas visuales (chips TAB / M, opcionales)
- Atajo global configurable (por defecto `Ctrl+Alt+M`)
- Icono de bandeja, inicio con Windows, tema claro/oscuro, colores de acento
- Compatible con múltiples monitores y DPI (PerMonitorV2)
- Las sesiones se agrupan por nombre de proceso y se ordenan por última actividad

## Uso

1. Ejecuta EarClarinet (aparece el icono en la bandeja).
2. Pulsa `Ctrl+Alt+M` para mostrar/ocultar el mezclador en el monitor del cursor.
3. Pasa el cursor por una barra y usa la **rueda del ratón**, **arrastra
   horizontalmente** o el teclado.

| Tecla          | Acción                         |
| -------------- | ------------------------------ |
| `Ctrl+Alt+M`   | Mostrar/ocultar el mezclador   |
| `↑` / `↓`      | Ciclar el foco                 |
| `←` / `→`      | Volumen − / +                  |
| `RePág/AvPág`  | Ajuste fino de volumen         |
| `M` / `Espacio`| Silenciar la app enfocada      |
| `Tab`          | Ciclar el foco (alternativo)   |
| `Esc`          | Cerrar el mezclador            |

## Instalación

Descarga el instalador de la última versión desde
[Releases](https://github.com/gatacampestre/EarClarinet/releases) (o ejecuta el
exe portable). La compilación es autocontenida — no requiere el runtime de .NET.

## Compilar

```powershell
dotnet build EarClarinet.slnx -c Release
dotnet test tests/EarClarinet.Tests/EarClarinet.Tests.csproj -c Release
```

Requiere el SDK de .NET 10 en Windows 10/11.

## Licencia

MIT — ver [LICENSE](LICENSE).

Si EarClarinet te resulta útil, considera apoyar su desarrollo:
[ko-fi.com/gatacampestre](https://ko-fi.com/gatacampestre)
