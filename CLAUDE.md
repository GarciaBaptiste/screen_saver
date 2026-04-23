# ScreenSaver Custom — CLAUDE.md

## Ce que c'est

Économiseur d'écran standalone pour Windows 11. Deux fenêtres WPF plein écran :
- **Écran principal** (2K paysage) : horloge analogique Braun + filigrane numérique HH:mm
- **Écran secondaire** (1080p portrait) : calendrier jour/mois

Pas un `.scr` natif — un `.exe` standalone qui gère lui-même l'idle detection et le multi-monitor.

## Stack

- **.NET 8 / WPF** — pas de framework MVVM externe
- **P/Invoke Win32** — centralisé dans `Native/Win32.cs`
- **Windows.Media.Control (WinRT/SMTC)** — détection lecture vidéo navigateur/media players
- **System.Text.Json** — config
- Publication : `dotnet publish -r win-x64 --self-contained`
- TFM : `net8.0-windows10.0.19041.0` (requis pour WinRT)

## État actuel — Phase 2 terminée

**Phase 1 ✅** : MVP complet et fonctionnel.

**Phase 2 ✅** : Polish visuel skeuomorphique + animation d'entrée.
- Filigrane numérique HH:mm sur `OverlayCanvas` (hors Viewbox → ClearType natif)
- Aiguille secondes fluide via `now.Millisecond / 1000.0`
- Marqueurs neumorphiques (emboss highlight/shadow + surface BackgroundBrush)
- Aiguilles avec contour plastique + DropShadow ajusté par aiguille
- Dot central soudé à l'aiguille secondes via couche `secCover`
- Texture grain film `WriteableBitmap` sur les deux fenêtres
- Tokens couleurs affinés : `ClockWhiteBrush` dédié horloge, AccentColor rouge-brun
- Animation d'entrée séquencée (voir § Animation d'entrée)

**Phase 3 🔜** : Données externes calendrier.

## Architecture

```
AppController
├── MonitorManager   → topologie dual/single, WM_DISPLAYCHANGE (filtre les faux positifs)
├── IdleWatcher      → polling GetLastInputInfo 1 s + MediaInhibitor (SMTC)
├── ThemeService     → swap ResourceDictionary dark/light
├── ConfigService    → lit config.json (copié auto dans bin/ à chaque build)
├── ClockWindow      → primary monitor, Cursor=None, hotkey Ctrl+Alt+S
└── CalendarWindow   → secondary monitor (ou moitié droite en single)
```

## Topologies moniteur

| État | Topologie | ClockWindow | CalendarWindow |
|---|---|---|---|
| 2 écrans actifs | `DualMonitor` | plein écran primary 2K | plein écran secondary 1080p portrait |
| 1 écran (Win+P) | `SingleMonitor` | moitié gauche du primary | moitié droite du primary |

`MonitorManager` écoute `WM_DISPLAYCHANGE` et ne reconstruit les fenêtres que si le nombre de moniteurs ou la topologie dual/single a réellement changé (évite les flashs sur WM_DISPLAYCHANGE spurieux).

## Détection idle et inhibition média

`IdleWatcher` combine deux sources :
1. `GetLastInputInfo` (polling 1 s) — souris ET clavier
2. `MediaInhibitor` (polling 5 s) — SMTC API : si une session est `Playing`, idle suspendu

`MediaInhibitor.InitializeAsync()` est lancé en fire-and-forget au démarrage (SMTC peut bloquer indéfiniment sur certaines machines). `idle.Start()` est appelé immédiatement après, sans attendre SMTC.

Grace period de 600 ms sur `MouseMove` après ouverture des fenêtres (WPF génère un MouseMove synthétique quand une fenêtre apparaît sous le curseur). Les clics et touches sont toujours immédiats.

## Hotkey manuel

`Ctrl+Alt+S` — toggle on/off. Enregistré via `RegisterHotKey` sur le HWND de la fenêtre message de `MonitorManager`. F15 n'est pas utilisable (touche inexistante sur les claviers PC standards).

## Config (`config.json`, à côté de l'exe)

```json
{
  "idle_threshold_seconds": 120,
  "theme": "dark",
  "clock": {
    "show_digital_watermark": true,
    "watermark_opacity": 0.12,
    "font_family": "Segoe UI Light"
  },
  "calendar": {
    "show_month_grid": true,
    "first_day_of_week": "Monday"
  }
}
```

`config.json` est déclaré dans le `.csproj` avec `CopyToOutputDirectory=PreserveNewest` — éditer le fichier à la racine du projet, il sera copié dans `bin/` à chaque build.

## Design — Inspiration Dieter Rams / Braun

### Tokens WPF (ResourceDictionary)

**Thème sombre** (`Theme.Dark.xaml`) :
| Clé | Hex | Usage |
|---|---|---|
| `BackgroundBrush` | `#1A1918` | Fond fenêtres |
| `FaceBrush` | `#252523` | Cadran horloge sombre |
| `TextPrimaryBrush` | `#F0EDE4` | Graduations longues, labels |
| `TextOnDarkBrush` | `#F0EDE4` | Textes calendrier |
| `HandBrush` | `#F0EDE4` | Aiguilles heures et minutes |
| `ClockWhiteBrush` | `#BAB7B0` | Blanc cassé dédié horloge (aiguilles, marqueurs, filigrane) |
| `AccentBrush` | `#BF4E16` | Aiguille secondes, dot central |
| `MutedBrush` | `#9E9B94` | Graduations courtes, textes secondaires |

**Thème clair** (`Theme.Light.xaml`) :
| Clé | Hex | Usage |
|---|---|---|
| `BackgroundBrush` | `#E8E4DC` | Fond fenêtres |
| `FaceBrush` | `#FAFAF7` | Cadran horloge clair |
| `TextPrimaryBrush` | `#1C1C1A` | Graduations longues |
| `HandBrush` | `#1C1C1A` | Aiguilles heures et minutes |
| `ClockWhiteBrush` | `#383533` | Sombre cassé dédié horloge |
| `AccentBrush` | `#BF4E16` | Aiguille secondes |
| `MutedBrush` | `#B0ADA6` | Graduations courtes, textes secondaires |

### Horloge (Canvas 500×500 logical, scalé par Viewbox)

- Cadran : `Ellipse` rayon 228, couleur `FaceBrush`
- 60 graduations neumorphiques : relief emboss (highlight α20 + shadow α85, offset = strokeW/2)
  - 12 longues : contour `ClockWhiteBrush` + fill `BackgroundBrush`
  - 48 courtes : fill `BackgroundBrush` uniquement
- Aiguilles : `Rectangle` arrondis, contour `HandOutline` (noir α55 → effet plastique)
  - Heures : 12×110, `ClockWhiteBrush`, DropShadow blur14 op0.62
  - Minutes : 8×155, `ClockWhiteBrush`, DropShadow blur14 op0.62
  - Secondes : 3×227, `AccentBrush`, DropShadow blur10 op0.80
- Dot central : rayon 14, `AccentBrush`
- `secCover` : Rectangle AccentBrush sans stroke, au-dessus du dot → soudure aiguille/dot invisible
- Timer : `DispatcherTimer` 100 ms, `UpdateHandAngles()` inclut `Millisecond / 1000.0` (rotation fluide)
- Filigrane numérique : `OverlayCanvas` hors Viewbox, 4 TextBlocks HH/MM en 2×2 grid, `ClockWhiteBrush`

### Calendrier

- Portrait (secondaire plein écran) : jour semaine muted, grand numéro (280 px), mois/année muted, grille mois
- Compact (single-monitor, moitié droite) : layout horizontal, tailles réduites
- Week-ends : `MutedBrush` (ni AccentBrush ni opacité réduite)

### Animation d'entrée

Les deux fenêtres utilisent `AllowsTransparency=True`, `Background=Transparent`. Un `BackgroundRect` (DynamicResource BackgroundBrush) et le `GrainOverlay` démarrent à `Opacity=0`.

**ClockWindow** :
| t | Événement |
|---|---|
| 0 → 700 ms | `Window.Opacity` 0→1 : 3 aiguilles sur fond transparent (bureau visible) |
| 700 → 1500 ms | `BackgroundRect` + `GrainOverlay` 0→1, puis `AnalogClock.StartReveal()` |
| 700 → 1500 ms | Marqueurs (emboss + petits + grands) + filigrane fade-in simultané (800 ms) |

**CalendarWindow** (fonds synchronisés avec ClockWindow) :
| t | Événement |
|---|---|
| 0 → 700 ms | `Window.Opacity` 0→1 : fenêtre transparente |
| 700 → 1500 ms | `BackgroundRect` + `GrainOverlay` 0→1 |
| 1500 ms | `CalendarView.StartReveal()` — contenu commence après le fond |
| 1500 ms + i×220 ms | Section i fade-in 600 ms (jour semaine → numéro → mois/année → grille) |

## Structure fichiers

```
screen_saver/
├── plan.md
├── CLAUDE.md
├── setup-autostart.ps1              ← Task Scheduler registration (-Remove pour désinstaller)
├── ScreenSaver.sln
└── ScreenSaver/
    ├── App.xaml / App.xaml.cs
    ├── AppController.cs
    ├── config.json
    ├── app.manifest
    ├── Native/
    │   └── Win32.cs                 ← GetWindowLong, SetWindowLong, SetWindowPos,
    │                                   RegisterHotKey, UnregisterHotKey, InitToolWindow()
    ├── Core/
    │   ├── MonitorManager.cs
    │   ├── IdleWatcher.cs
    │   ├── MediaInhibitor.cs
    │   ├── ThemeService.cs
    │   ├── ConfigService.cs
    │   └── GrainHelper.cs           ← WriteableBitmap bruit → ImageBrush grain film
    ├── Models/
    │   ├── MonitorInfo.cs
    │   ├── MonitorTopology.cs
    │   └── AppConfig.cs
    ├── Themes/
    │   ├── Theme.Dark.xaml
    │   └── Theme.Light.xaml
    ├── Windows/
    │   ├── ClockWindow.xaml / .cs   ← GrainOverlay Rectangle en dernier dans Grid
    │   └── CalendarWindow.xaml / .cs
    ├── Controls/
    │   ├── AnalogClock.xaml / .cs   ← OverlayCanvas pour filigrane natif ClearType
    │   └── CalendarView.xaml / .cs
    └── Providers/
        └── ICalendarDataProvider.cs ← Phase 3
```

## Contraintes importantes

- **DPI** : manifest `PerMonitorV2` — ne jamais utiliser `Window.Left/Top` pour positionner, toujours `SetWindowPos` avec les bounds physiques de `MonitorManager`
- **Fenêtres** : `WindowStyle=None`, `ResizeMode=NoResize`, `Topmost=true`, `ShowInTaskbar=False`, `Cursor=None`
- **Alt+Tab** : `WS_EX_TOOLWINDOW` appliqué via `Win32.InitToolWindow()` dans `SourceInitialized`
- **Positionnement** : toujours via bounds physiques de `MonitorManager`, jamais `WindowState=Maximized`
- **SMTC** : fire-and-forget obligatoire — `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()` peut bloquer
- **WM_DISPLAYCHANGE** : comparer avant/après Refresh() avant de notifier `TopologyChanged`
- **AllowsTransparency** : `True` sur les deux fenêtres — ne jamais remettre à `False` (casse la phase 1 transparente)
- **Filigrane** : TextBlocks sur `OverlayCanvas` (hors Viewbox) — ne jamais remettre dans le Viewbox (rendu flou)
- **Pas de NuGet externe** sauf accord explicite

## Roadmap — état

- **Phase 1** ✅ MVP fonctionnel (terminée)
- **Phase 2** ✅ Polish visuel skeuomorphique (terminée)
- **Phase 3** 🔜 Données externes calendrier
- **Phase 4** UI de configuration + tray icon
