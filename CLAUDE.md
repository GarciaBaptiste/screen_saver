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
- **System.Windows.Forms** — `NotifyIcon` tray icon uniquement (`UseWindowsForms=true` ; global usings `System.Drawing` et `System.Windows.Forms` supprimés pour éviter les conflits WPF)
- **System.Text.Json** — config
- Publication : `dotnet publish -r win-x64 --self-contained`
- TFM : `net8.0-windows10.0.19041.0` (requis pour WinRT)

## État actuel — Phase 4 + UX terminées

**Phase 1 ✅** : MVP complet et fonctionnel.

**Phase 2 ✅** : Polish visuel skeuomorphique + animation d'entrée.
- Filigrane numérique HH:mm sur `OverlayCanvas` (hors Viewbox → ClearType natif)
- Aiguille secondes fluide via `now.Millisecond / 1000.0`
- Marqueurs neumorphiques (emboss highlight/shadow + surface BackgroundBrush)
- Aiguilles avec contour plastique + DropShadow ajusté par aiguille
- Dot central soudé à l'aiguille secondes via couche `secCover`
- Texture grain film `WriteableBitmap` sur les deux fenêtres
- Tokens couleurs affinés : `ClockWhiteBrush` dédié horloge, AccentColor configurable
- Animation d'entrée séquencée (voir § Animation d'entrée)

**Phase 3 🔜** : Données externes calendrier.

**Phase 4 ✅** : Icône système + panneau de contrôle.
- `System.Windows.Forms.NotifyIcon` — WinForms intégré .NET 8 Windows, zéro NuGet externe
- Icône horloge dessinée en GDI+ (cadran + aiguilles + dot AccentBrush 32×32 px)
- Menu contextuel (clic droit) : Activer/Masquer · Paramètres… · Quitter
- **Clic gauche** sur l'icône → ouvre `SettingsWindow` directement
- Libellé Activer/Masquer mis à jour dynamiquement à l'ouverture du menu
- Si le screensaver est actif à l'ouverture des paramètres, il est fermé d'abord
- `SettingsWindow` : voir § SettingsWindow ci-dessous
- Enregistrer applique le thème et le seuil idle à chaud ; fenêtres clock/calendar au prochain cycle

**Phase 4 UX ✅** : Améliorations panneau paramètres + architecture couleurs.
- Refonte complète de `SettingsWindow` (swatches, pills, toggles — zéro dropdown)
- Architecture couleurs unifiée : `Palette.xaml` source unique, `ThemeService` écrit les brushes dynamiquement
- Couleur tonique configurable (4 couleurs + mode aléatoire)
- Thème clair : aiguilles et marqueurs blancs/crème (`#F0EDE4`)

## Architecture

```
AppController
├── MonitorManager   → topologie dual/single, WM_DISPLAYCHANGE (filtre les faux positifs)
├── IdleWatcher      → polling GetLastInputInfo 1 s + MediaInhibitor (SMTC)
├── ThemeService     → écrit les brushes dynamiques dans Application.Current.Resources
├── ConfigService    → lit config.json (copié auto dans bin/ à chaque build)
├── ClockWindow      → primary monitor, Cursor=None, hotkey Ctrl+Alt+S
└── CalendarWindow   → secondary monitor (ou moitié droite en single)
```

## Topologies moniteur

| État | Topologie | ClockWindow | CalendarWindow |
|---|---|---|---|
| 2 écrans actifs | `DualMonitor` | plein écran primary 2K | plein écran secondary 1080p portrait |
| 1 écran (Win+P) | `SingleMonitor` | 2/3 gauche du primary | 1/3 droit du primary (layout portrait) |

`MonitorManager` écoute `WM_DISPLAYCHANGE` et ne reconstruit les fenêtres que si le nombre de moniteurs ou la topologie dual/single a réellement changé (évite les flashs sur WM_DISPLAYCHANGE spurieux).

## Détection idle et inhibition média

`IdleWatcher` combine deux sources :
1. `GetLastInputInfo` (polling 1 s) — souris ET clavier
2. `MediaInhibitor` (polling 5 s) — SMTC API : inhibe seulement si une session `Playing` a `PlaybackType.Video`

Musique seule (Apple Music, Spotify, etc.) ne bloque **pas** le screensaver. `MediaInhibitor` exclut explicitement les apps audio connues via `_audioAppKeywords` — certaines (Apple Music) déclarent `PlaybackType.Video` pour leur rendu d'album art animé.

`MediaInhibitor.InitializeAsync()` est lancé en fire-and-forget au démarrage (SMTC peut bloquer indéfiniment sur certaines machines). `idle.Start()` est appelé immédiatement après, sans attendre SMTC.

Grace period de 600 ms sur `MouseMove` après ouverture des fenêtres (WPF génère un MouseMove synthétique quand une fenêtre apparaît sous le curseur). Les clics et touches sont toujours immédiats.

`OnTopologyChanged` (Win+P, déconnexion moniteur) appelle `ForceActivity()` quand le screensaver est fermé — garantit que `_isIdle` et le timer sont dans un état propre après un changement de topologie.

## Hotkey manuel

`Ctrl+Alt+S` — toggle on/off. Enregistré via `RegisterHotKey` sur le HWND de la fenêtre message de `MonitorManager`. F15 n'est pas utilisable (touche inexistante sur les claviers PC standards).

## Config (`config.json`, à côté de l'exe)

```json
{
  "idle_threshold_seconds": 120,
  "theme": "dark",
  "accent_color": "#E93F29",
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

`accent_color` accepte `"random"` → couleur re-randomisée parmi les 4 accents à chaque activation.

`config.json` est déclaré dans le `.csproj` avec `CopyToOutputDirectory=PreserveNewest` — éditer le fichier à la racine du projet, il sera copié dans `bin/` à chaque build.

## SettingsWindow

Interface sans dropdown — tout en swatches, pills ou toggles.

**GÉNÉRAL**
- Slider délai inactivité avec ticks non-uniformes : 5 s, 10 s, 30 s, 1 min, 1 min 30, 2 min, 5 min, 10 min, 15 min, 20 min, 30 min, 45 min, 1 h
- Swatches thème : 2 ronds (dark #0E0D0C / light #E8E4DC)
- Swatches couleur tonique : 4 couleurs + 1 swatch 4-quadrants (mode aléatoire). La couleur s'applique en live ; Cancel restaure l'original.

**HORLOGE**
- Toggle on/off filigrane numérique

**CALENDRIER**
- Toggle grille du mois
- Pills premier jour de semaine (Lundi / Dimanche)

## Architecture couleurs

**Source unique** : `Themes/Palette.xaml`
```xml
<Color x:Key="Black">#0E0D0C</Color>   <!-- fond dark / texte light -->
<Color x:Key="White">#F0EDE4</Color>   <!-- texte dark / fond light -->
<Color x:Key="Accent1">#E93F29</Color>
<Color x:Key="Accent2">#EEA929</Color>
<Color x:Key="Accent3">#6518EA</Color>
<Color x:Key="Accent4">#00A745</Color>
<!-- PanelBgBrush, PanelSurfaceBrush, PanelTextBrush, PanelMutedBrush, PanelBorderBrush -->
```

`ThemeService.Apply(theme)` lit `Black` et `White` depuis `Application.Current.Resources` (chargés par Palette.xaml) et écrit les brushes dynamiques. Changer `Black` ou `White` dans Palette.xaml se répercute immédiatement au prochain `Apply()`.

Les fichiers `Theme.Dark.xaml` et `Theme.Light.xaml` ont été **supprimés**.

**Tokens runtime** (écrits par `ThemeService`) :

| Clé | Thème sombre | Thème clair |
|---|---|---|
| `BackgroundBrush` | `Black` (#0E0D0C) | #E8E4DC |
| `FaceBrush` | #252523 | #FAFAF7 |
| `TextPrimaryBrush` | `White` | #1C1C1A |
| `TextOnDarkBrush` | `White` | #1C1C1A |
| `HandBrush` | `White` | `White` (#F0EDE4) |
| `ClockWhiteBrush` | #BAB7B0 | `White` (#F0EDE4) |
| `MutedBrush` | #9E9B94 | #B0ADA6 |
| `AccentBrush` / `AccentColor` | couleur tonique choisie | idem |

**Panel settings** (fixes, même dans les deux thèmes) : `PanelBgBrush` #3A3A3A, `PanelSurfaceBrush` #444444, `PanelTextBrush` #E0DDD8, `PanelMutedBrush` #888480, `PanelBorderBrush` #505050.

## Design — Inspiration Dieter Rams / Braun

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

- Layout portrait unique (dual et single) : jour semaine muted, grand numéro (280 px), mois/année muted, grille mois
- Week-ends : `MutedBrush` (ni AccentBrush ni opacité réduite)
- `CalendarWindow` n'a pas de paramètre `isCompact` — le layout paysage compact a été supprimé
- `CalendarView.Refresh()` vérifie `ShowMonthGrid` avant de construire/afficher la grille

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
├── CLAUDE.md
├── setup-autostart.ps1              ← Task Scheduler registration (-Remove pour désinstaller)
├── ScreenSaver.sln
└── ScreenSaver/
    ├── App.xaml / App.xaml.cs       ← charge Palette.xaml ; AccentColors[], ResolveAccent()
    ├── AppController.cs             ← re-randomise accent si mode aléatoire à chaque activation
    ├── config.json
    ├── app.manifest
    ├── Native/
    │   └── Win32.cs                 ← GetWindowLong, SetWindowLong, SetWindowPos,
    │                                   RegisterHotKey, UnregisterHotKey, InitToolWindow()
    ├── Core/
    │   ├── MonitorManager.cs
    │   ├── IdleWatcher.cs
    │   ├── MediaInhibitor.cs
    │   ├── ThemeService.cs          ← écrit brushes dans Application.Current.Resources
    │   ├── ConfigService.cs
    │   └── GrainHelper.cs           ← WriteableBitmap bruit → ImageBrush grain film
    ├── Models/
    │   ├── MonitorInfo.cs
    │   ├── MonitorTopology.cs
    │   └── AppConfig.cs             ← AccentColor property ajoutée
    ├── Themes/
    │   └── Palette.xaml             ← SEUL fichier couleurs (Theme.Dark/Light.xaml supprimés)
    ├── Windows/
    │   ├── ClockWindow.xaml / .cs   ← GrainOverlay Rectangle en dernier dans Grid
    │   ├── CalendarWindow.xaml / .cs
    │   └── SettingsWindow.xaml / .cs ← refonte complète UX (swatches, pills, toggles)
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
- **Couleurs** : ne jamais recréer Theme.Dark.xaml / Theme.Light.xaml — tout passe par Palette.xaml + ThemeService
- **Pas de NuGet externe** sauf accord explicite

## Roadmap — état

- **Phase 1** ✅ MVP fonctionnel (terminée)
- **Phase 2** ✅ Polish visuel skeuomorphique (terminée)
- **Phase 3** 🔜 Données externes calendrier
- **Phase 4** ✅ Icône système + panneau de contrôle (terminée)
- **Phase 4 UX** ✅ Refonte SettingsWindow + architecture couleurs unifiée (terminée)
