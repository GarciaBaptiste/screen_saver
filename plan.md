# ScreenSaver Custom — Plan de projet

## Vue d'ensemble

Économiseur d'écran standalone pour Windows 11, deux moniteurs avec contenu différencié :
- **Écran principal** : horloge analogique avec aiguilles + filigrane numérique HH:mm
- **Écran secondaire** : calendrier (jour/mois), extensible vers des données externes

Style : skeuomorphisme inspiré Dieter Rams / Braun — fonctionnel, épuré, sans ornement superflu.

---

## Configuration matérielle cible

| Écran | Résolution | Orientation | Rôle |
|---|---|---|---|
| Principal | 2560×1440 (2K) | Paysage | Horloge |
| Secondaire | 1080×1920 (1080p) | Portrait (rotation 90°) | Calendrier |

Le secondaire peut être désactivé via **Win+P** (software, sans débranchement physique).
Dans ce cas Windows le retire de `EnumDisplayMonitors` → l'app passe en **mode single-monitor**.

---

## Décisions d'architecture

| Choix | Décision | Raison |
|---|---|---|
| Intégration Windows | **Standalone `.exe`** | Multi-monitor différencié impossible proprement en .scr natif |
| Stack | **WPF / C# / .NET 8** | Natif Windows, multi-fenêtre sans friction, P/Invoke Win32, publication auto-contenue |
| Déclenchement idle | **In-app** via `GetLastInputInfo` + inhibition média | Contrôle total du délai, détection lecture vidéo |
| Démarrage auto | **Planificateur de tâches Windows** | Propre, sans service, sans droits admin permanents |
| Configuration | **JSON** (`config.json`) | Simple, éditable à la main, pas de registry |
| Infos moniteurs | **`EnumDisplayMonitors` + `GetMonitorInfo`** (Win32) | Lit directement résolution, orientation, position depuis Windows |

---

## Stack technique

```
.NET 8 (WPF)
├── Rendu UI          → WPF (XAML + code-behind)
├── Dessin horloge    → WPF Canvas + Rectangle + RotateTransform (vectoriel)
├── Win32 interop     → P/Invoke centralisé dans Native/Win32.cs
├── Media detection   → Windows.Media.Control (SMTC) — API WinRT, incluse dans .NET 8
├── Config            → System.Text.Json
└── Publication       → dotnet publish -r win-x64 --self-contained
```

---

## Structure de fichiers

```
screen_saver/
├── plan.md
├── CLAUDE.md
├── setup-autostart.ps1              ← Task Scheduler registration (-Remove pour désinstaller)
├── ScreenSaver.sln
└── ScreenSaver/
    ├── App.xaml / App.xaml.cs
    ├── AppController.cs
    ├── config.json                  ← CopyToOutputDirectory=PreserveNewest
    ├── app.manifest                 ← PerMonitorV2 DPI awareness
    ├── Native/
    │   └── Win32.cs                 ← toutes les déclarations P/Invoke
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
    │   ├── ClockWindow.xaml / .cs
    │   └── CalendarWindow.xaml / .cs
    ├── Controls/
    │   ├── AnalogClock.xaml / .cs
    │   └── CalendarView.xaml / .cs
    └── Providers/
        └── ICalendarDataProvider.cs
```

---

## Roadmap par phases

### Phase 1 — MVP fonctionnel ✅ TERMINÉE

- [x] Setup solution .NET 8 WPF + manifest DPI PerMonitorV2
- [x] `MonitorManager` : détection topologie dual/single + `WM_DISPLAYCHANGE`
- [x] `MediaInhibitor` : intégration SMTC, détection lecture en cours (fire-and-forget au démarrage)
- [x] `IdleWatcher` : polling idle + inhibition media + wake sur mouse/keyboard
- [x] `AppController` : cycle de vie + reconstruction sur changement topologie réel
- [x] `ConfigService` : lecture JSON (copie auto vers bin/ via CopyToOutputDirectory)
- [x] `ThemeService` : dark/light via ResourceDictionary swap
- [x] `ClockWindow` : horloge analogique, cadran sombre, aiguilles H/M/S, curseur caché
- [x] `CalendarWindow` : jour / mois / année + grille, layout adaptatif portrait/paysage compact
- [x] `Native/Win32.cs` : centralisation des P/Invoke (GetWindowLong, SetWindowLong, SetWindowPos, RegisterHotKey, UnregisterHotKey)
- [x] Hotkey manuel **Ctrl+Alt+S** (toggle on/off)
- [x] Grace period 600 ms pour MouseMove synthétique WPF
- [x] WM_DISPLAYCHANGE : reconstruction uniquement si topologie réellement changée

### Phase 2 — Polish visuel ✅ TERMINÉE

- [x] **Filigrane numérique HH:mm** : `OverlayCanvas` hors Viewbox → rendu ClearType natif (4 TextBlocks en 2×2 grid, `ClockWhiteBrush`)
- [x] **Aiguille secondes fluide** : rotation continue via `now.Millisecond / 1000.0` dans `totalSeconds`
- [x] **Marqueurs neumorphiques** : emboss highlight/shadow (deux lignes offset+blur) + surface `BackgroundBrush`
  - Gros marqueurs : contour `ClockWhiteBrush` + fill fond
  - Petits marqueurs : fill fond uniquement
- [x] **Aiguilles avec contour plastique** : `HandOutline` (noir α55) + `DropShadowEffect` ajusté par aiguille
  - Heures : 12×110, blur14 op0.62 — Minutes : 8×155, blur14 op0.62 — Secondes : 3×227, blur10 op0.80
- [x] **Dot central soudé** : `secCover` Rectangle (AccentBrush, sans stroke) au-dessus du dot → démarcation invisible
- [x] **Grain film** : `GrainHelper` génère `WriteableBitmap` BGRA bruit tuilé (opacité 0.015) sur les deux fenêtres
- [x] **Tokens couleurs** : `ClockWhiteBrush` dédié horloge (#BAB7B0 dark / #383533 light), `AccentColor` #BF4E16, fond dark #1A1918
- [x] **Calendrier** : week-ends `MutedBrush`, `TextFormattingMode=Display` sur grands TextBlocks
- [x] **Script démarrage auto** : `setup-autostart.ps1` (Task Scheduler, trigger logon, -Remove pour désinstaller)
- [x] **Animation d'entrée** : `AllowsTransparency=True` sur les deux fenêtres
  - Phase 1 (700 ms) : aiguilles sur fond transparent, bureau visible en dessous
  - Phase 2 (800 ms) : fond + grain fade-in simultané sur les deux écrans
  - Post-fond horloge : marqueurs + filigrane fade-in ensemble
  - Post-fond calendrier : sections en cascade (stagger 220 ms, fade 600 ms chacune)

### Phase 3 — Données externes calendrier 🔜

- [ ] Interface `ICalendarDataProvider`
- [ ] Provider journées internationales (API publique)
- [ ] Provider événements astronomiques
- [ ] Provider iCal/.ics (Outlook / Google Calendar)

### Phase 4 — UI de configuration (optionnel)

- [ ] Tray icon (icône dans la barre système)
- [ ] Fenêtre de paramètres simple
- [ ] Prévisualisation du thème en temps réel

---

## Design system — Inspiration Dieter Rams / Braun

### Principes
- **Moins, mais mieux** : chaque élément visible doit avoir une fonction
- Skeuomorphisme subtil : ombres douces, reliefs neumorphiques, grain film — jamais tape-à-l'œil

### Palette — Thème sombre

| Token / Clé WPF | Hex | Usage |
|---|---|---|
| `BackgroundBrush` | `#1A1918` | Fond des fenêtres |
| `FaceBrush` | `#252523` | Cadran horloge (sombre, légèrement plus clair que le fond) |
| `TextPrimaryBrush` | `#F0EDE4` | Graduations longues, labels — off-white sur fond sombre |
| `TextOnDarkBrush` | `#F0EDE4` | Textes calendrier (numéro du jour) |
| `HandBrush` | `#F0EDE4` | Aiguilles heures et minutes |
| `ClockWhiteBrush` | `#BAB7B0` | Blanc cassé dédié horloge (découplé du calendrier) |
| `AccentBrush` | `#BF4E16` | Aiguille secondes, dot central — orange rouge-brun |
| `MutedBrush` | `#9E9B94` | Graduations courtes, textes secondaires calendrier |

### Palette — Thème clair

| Token / Clé WPF | Hex | Usage |
|---|---|---|
| `BackgroundBrush` | `#E8E4DC` | Fond des fenêtres |
| `FaceBrush` | `#FAFAF7` | Cadran horloge (clair) |
| `TextPrimaryBrush` | `#1C1C1A` | Graduations longues, labels sombres sur cadran clair |
| `TextOnDarkBrush` | `#1C1C1A` | Textes calendrier |
| `HandBrush` | `#1C1C1A` | Aiguilles heures et minutes |
| `ClockWhiteBrush` | `#383533` | Sombre cassé dédié horloge |
| `AccentBrush` | `#BF4E16` | Aiguille secondes |
| `MutedBrush` | `#B0ADA6` | Graduations courtes, textes secondaires |

### Typographie
- Filigrane horloge : Helvetica Neue (fallback : Segoe UI), Normal weight
- Calendrier chiffres : Segoe UI Light
- Pas de serif

### Horloge — constantes visuelles (Canvas 500×500)

| Élément | Constante | Valeur |
|---|---|---|
| Rayon cadran | `FaceR` | 228 |
| Aiguille heures | `HourHandW/H` | 12 × 110 |
| Aiguille minutes | `MinHandW/H` | 8 × 155 |
| Aiguille secondes | `SecHandW/H` | 3 × 227 |
| Dot central | `CenterDotR` | 14 |

---

## Comportements documentés

| Situation | Comportement | Notes |
|---|---|---|
| Vidéo YouTube muette | Screensaver peut se déclencher | SMTC ne remonte pas vidéo sans audio |
| Win+P (désactivation secondaire) | Passage automatique en mode single | Via WM_DISPLAYCHANGE |
| Ctrl+Alt+S | Toggle on/off manuel du screensaver | Hotkey global RegisterHotKey |
| MouseMove à l'ouverture | Ignoré 600 ms (grace period) | Évite le MouseMove synthétique WPF |
| SMTC bloquant au démarrage | Fire-and-forget, idle démarre immédiatement | GlobalSystemMediaTransportControlsSessionManager.RequestAsync peut bloquer |
| WM_DISPLAYCHANGE spurieux | Ignoré si topologie inchangée | Évite flash de bureau lors de notifications/DWM |
