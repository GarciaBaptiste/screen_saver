# ScreenSaver Custom — CLAUDE.md

## Ce que c'est

Économiseur d'écran standalone pour Windows 11. Deux fenêtres WPF plein écran :
- **Écran principal** (2K paysage) : horloge analogique Braun + filigrane numérique (Phase 2)
- **Écran secondaire** (1080p portrait) : calendrier jour/mois

Pas un `.scr` natif — un `.exe` standalone qui gère lui-même l'idle detection et le multi-monitor.

## Stack

- **.NET 8 / WPF** — pas de framework MVVM externe
- **P/Invoke Win32** — centralisé dans `Native/Win32.cs`
- **Windows.Media.Control (WinRT/SMTC)** — détection lecture vidéo navigateur/media players
- **System.Text.Json** — config
- Publication : `dotnet publish -r win-x64 --self-contained`
- TFM : `net8.0-windows10.0.19041.0` (requis pour WinRT)

## État actuel — Phase 1 terminée, Phase 2 à démarrer

**Phase 1 ✅** : MVP complet et fonctionnel. Tout ce qui est décrit ci-dessous est implémenté et testé.

**Phase 2 🔜** : Polish visuel. Voir `plan.md` § "Phase 2" pour le détail.
Les trois tâches prioritaires :
1. Filigrane numérique `HH:mm:ss` derrière le cadran horloge (opacité ~12 %)
2. Aiguille secondes fluide (interpolation continue incluant millisecondes)
3. Vérification DPI fine sur le 2K pour le canvas et le text rendering

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
| `BackgroundBrush` | `#1C1C1A` | Fond fenêtres |
| `FaceBrush` | `#252523` | Cadran horloge sombre |
| `TextPrimaryBrush` | `#F0EDE4` | Graduations longues, labels |
| `TextOnDarkBrush` | `#F0EDE4` | Textes calendrier |
| `HandBrush` | `#F0EDE4` | Aiguilles heures et minutes |
| `AccentBrush` | `#E85D00` | Aiguille secondes, dot central |
| `MutedBrush` | `#9E9B94` | Graduations courtes, textes secondaires |

**Thème clair** (`Theme.Light.xaml`) :
| Clé | Hex | Usage |
|---|---|---|
| `BackgroundBrush` | `#E8E4DC` | Fond fenêtres |
| `FaceBrush` | `#FAFAF7` | Cadran horloge clair |
| `TextPrimaryBrush` | `#1C1C1A` | Graduations longues |
| `HandBrush` | `#1C1C1A` | Aiguilles heures et minutes |
| `AccentBrush` | `#E85D00` | Aiguille secondes |
| `MutedBrush` | `#B0ADA6` | Graduations courtes, textes secondaires |

### Horloge (Canvas 500×500 logical, scalé par Viewbox)

- Cadran : `Ellipse` rayon 228, couleur `FaceBrush`
- 60 graduations : 12 longues (épaisseur 2.5, longueur 22) + 48 courtes (1.2, 10)
- Aiguilles : `Rectangle` arrondis, pivot en bas centré sur `(250, 250)`
  - Heures : 9×135, `HandBrush`
  - Minutes : 6×185, `HandBrush`
  - Secondes : 3×210, `AccentBrush`
- Dot central : rayon 6, `AccentBrush`, dessiné en dernier (au-dessus des aiguilles)
- Timer : `DispatcherTimer` 100 ms, `UpdateHandAngles()` calcule à partir de `DateTime.Now`
- **Phase 2** : aiguille secondes fluide via `now.Millisecond / 1000.0` (déjà dans totalSeconds, vérifier)

### Calendrier

- Portrait (secondaire plein écran) : jour semaine muted, grand numéro (280 px), mois/année muted, grille mois
- Compact (single-monitor, moitié droite) : layout horizontal, tailles réduites

## Structure fichiers

```
ScreenSaver/
├── App.xaml / App.xaml.cs
├── AppController.cs
├── config.json
├── app.manifest
├── Native/
│   └── Win32.cs                     ← GetWindowLong, SetWindowLong, SetWindowPos,
│                                       RegisterHotKey, UnregisterHotKey, InitToolWindow()
├── Core/
│   ├── MonitorManager.cs
│   ├── IdleWatcher.cs
│   ├── MediaInhibitor.cs
│   ├── ThemeService.cs
│   └── ConfigService.cs
├── Models/
│   ├── MonitorInfo.cs
│   ├── MonitorTopology.cs
│   └── AppConfig.cs
├── Themes/
│   ├── Theme.Dark.xaml
│   └── Theme.Light.xaml
├── Windows/
│   ├── ClockWindow.xaml / .cs       ← Cursor=None, délègue à Win32.InitToolWindow
│   └── CalendarWindow.xaml / .cs    ← Cursor=None, délègue à Win32.InitToolWindow
├── Controls/
│   ├── AnalogClock.xaml / .cs       ← Phase 2 : filigrane + aiguille fluide
│   └── CalendarView.xaml / .cs
└── Providers/
    └── ICalendarDataProvider.cs     ← Phase 3
```

## Contraintes importantes

- **DPI** : manifest `PerMonitorV2` — ne jamais utiliser `Window.Left/Top` pour positionner, toujours `SetWindowPos` avec les bounds physiques de `MonitorManager`
- **Fenêtres** : `WindowStyle=None`, `ResizeMode=NoResize`, `Topmost=true`, `ShowInTaskbar=False`, `Cursor=None`
- **Alt+Tab** : `WS_EX_TOOLWINDOW` appliqué via `Win32.InitToolWindow()` dans `SourceInitialized`
- **Positionnement** : toujours via bounds physiques de `MonitorManager`, jamais `WindowState=Maximized`
- **SMTC** : fire-and-forget obligatoire — `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()` peut bloquer
- **WM_DISPLAYCHANGE** : comparer avant/après Refresh() avant de notifier `TopologyChanged`
- **Pas de NuGet externe** sauf accord explicite

## Roadmap — état

- **Phase 1** ✅ MVP fonctionnel (terminée)
- **Phase 2** 🔜 Polish visuel (prochaine)
- **Phase 3** Données externes calendrier
- **Phase 4** UI de configuration + tray icon
