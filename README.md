Project made for fun and testing of DSv4.

# DeckUPipes

A per-app volume mixer for Windows. Press a hotkey and a compact mixer appears
in the top-left or top-right corner of the screen: adjust each app's volume
independently, mute anything with one click, and see which app is playing audio.

## Features

- Per-app volume and mute via the Windows Core Audio API (no extra services)
- Overlay anchored to the **top-left or top-right** corner of the screen, as one consistent vertical composition
- System volume row with live peak meter
- TAB cycle between apps with visual hints (TAB / M chips, optional)
- Configurable global hotkey (default `Ctrl+Alt+M`)
- Tray icon, launch on startup, dark/light theme, accent color presets
- Multi-monitor aware, DPI aware (PerMonitorV2)
- Sessions are grouped by process name and ordered by last activity

## Usage

1. Run DeckUPipes (tray icon appears).
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
[Releases](https://github.com/Gatalina-delcampo/DeckUPipes/releases) (or run the
portable exe). The build is self-contained — no .NET runtime needed.

## Building

```powershell
dotnet build DeckUPipes.slnx -c Release
dotnet test tests/DeckUPipes.Tests/DeckUPipes.Tests.csproj -c Release
```

Requires the .NET 10 SDK on Windows 10/11.

## Skins

DeckUPipes looks for per-user skin packages under:

```text
%APPDATA%\DeckUPipes\skins\<skin-id>\skin.json
```

It also reads legacy packages from `%APPDATA%\EarClarinet\skins` during migration. Select a discovered skin from **Settings > Skin**. If the selected package is missing or invalid, DeckUPipes keeps using the built-in theme and reports the fallback in Settings.

Install a skin from **Settings > Skin**: press **Load skin...** and pick a `.zip` package, or **Folder...** for an unpacked folder - or drag either one onto the window. A card shows the skin id, how many objects were found and every problem it found before anything is copied; **Install skin** (or **Replace**) then switches the overlay to it immediately, with no restart. **Remove...** deletes an installed skin.

A package declares either `deckupipes-skin` or the pre-rename `earclarinet-skin`; both are accepted. A skin cannot set text colours, so pair a dark skin with the Dark theme.

A skin package uses the shared fixed-object contract:

```text
global-background.png   128 × 140   tiled global row surface
global-avatar.png       124 × 124   global identity
global-volume.png      48 × 48     repeated active volume segment
volume-background.png  520 × 60    volume control surface
global-mute.png        108 × 108   global mute control
program-background.png 128 × 90    tiled Program row surface
program-icon.png       84 × 84     frame drawn behind the real app icon
program-volume.png     48 × 48     repeated active volume segment
program-mute.png       68 × 68     shared Program mute control
connector.png          128 × 90     tiled connector texture
termination.png        756 × 42     one final stack endpoint
```

The skin editor generates the blank PNG templates and exports the manifest contract. Program rows reuse the same shared Program assets; they are not separate Browser/Music/Game artwork slots.

## License

MIT — see [LICENSE](LICENSE).

> **Tip**: if the hotkey does not respond while a game or overlay runs as
> administrator (e.g. League of Legends with Porofessor), launch DeckUPipes as
> administrator too.

---

# Español

Un mezclador de volumen por aplicación para Windows. Pulsa un atajo y aparece
un mezclador compacto en la esquina superior izquierda o derecha de la pantalla:
ajusta el volumen de cada aplicación de forma independiente, silencia cualquier
cosa con un clic y ve qué aplicación está reproduciendo audio.

## Funciones

- Volumen y silencio por aplicación vía Windows Core Audio API
- Overlay anclado a la **esquina superior izquierda o derecha**, como una única
  columna vertical
- Fila de volumen del sistema con medidor de pico en vivo
- Ciclo con Tab entre aplicaciones con pistas visuales (chips TAB / M, opcionales)
- Atajo global configurable (por defecto `Ctrl+Alt+M`)
- Icono de bandeja, inicio con Windows, tema claro/oscuro, colores de acento
- Compatible con múltiples monitores y DPI (PerMonitorV2)
- Las sesiones se agrupan por nombre de proceso y se ordenan por última actividad

## Uso

1. Ejecuta DeckUPipes (aparece el icono en la bandeja).
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
[Releases](https://github.com/Gatalina-delcampo/DeckUPipes/releases) (o ejecuta el
exe portable). La compilación es autocontenida — no requiere el runtime de .NET.

## Compilar

```powershell
dotnet build DeckUPipes.slnx -c Release
dotnet test tests/DeckUPipes.Tests/DeckUPipes.Tests.csproj -c Release
```

Requiere el SDK de .NET 10 en Windows 10/11.

## Licencia

MIT — ver [LICENSE](LICENSE).

> **Consejo**: si el atajo no responde mientras un juego o overlay corre como
> administrador (p. ej. League of Legends con Porofesor), ejecuta DeckUPipes
> también como administrador.



