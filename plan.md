# ScreenSaver Custom — Plan de projet

## Vue d'ensemble

Économiseur d'écran standalone pour Windows 11, deux moniteurs avec contenu différencié :
- **Écran principal** : horloge analogique avec aiguilles + cadran numérique en filigrane
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

## Structure de fichiers (état actuel)

```
screen_saver/
├── plan.md
├── CLAUDE.md
├── ScreenSaver.sln
└── ScreenSaver/
    ├── App.xaml / App.xaml.cs
    ├── AppController.cs
    ├── config.json                      ← CopyToOutputDirectory=PreserveNewest
    ├── app.manifest                     ← PerMonitorV2 DPI awareness
    ├── Native/
    │   └── Win32.cs                     ← toutes les déclarations P/Invoke
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

### Phase 2 — Polish visuel 🔜 PROCHAINE ÉTAPE

- [ ] **Filigrane numérique** : `HH:mm:ss` dessiné sur le Canvas derrière le cadran, opacité ~12 % (config : `show_digital_watermark`, `watermark_opacity`)
- [ ] **Aiguille secondes fluide** : interpolation continue via `DateTime.Now.Millisecond` — passer de 100 ms discret à rotation sub-frame lisse
- [ ] **DPI canvas** : vérifier que le canvas 500×500 dans son Viewbox scale correctement sur le 2K (text rendering, StrokeThickness)
- [ ] **Finition thème sombre** : proportions Braun, espacement typographique calendrier, hiérarchie visuelle
- [ ] **Finition thème clair** : cohérence palette, HandBrush adapté (dark on light face)
- [ ] **Démarrage auto** : script PowerShell de setup du Planificateur de tâches

### Phase 3 — Données externes calendrier

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
- Pas de dégradés décoratifs, pas d'ombres portées inutiles

### Palette — Thème sombre (implémenté)

| Token / Clé WPF | Hex | Usage |
|---|---|---|
| `BackgroundBrush` | `#1C1C1A` | Fond des fenêtres |
| `FaceBrush` | `#252523` | Cadran horloge (sombre, légèrement plus clair que le fond) |
| `TextPrimaryBrush` | `#F0EDE4` | Graduations longues, labels — off-white sur fond sombre |
| `TextOnDarkBrush` | `#F0EDE4` | Textes calendrier (numéro du jour) |
| `HandBrush` | `#F0EDE4` | Aiguilles heures et minutes |
| `AccentBrush` | `#E85D00` | Aiguille secondes, dot central, highlights |
| `MutedBrush` | `#9E9B94` | Graduations courtes, textes secondaires calendrier |

### Palette — Thème clair

| Token / Clé WPF | Hex | Usage |
|---|---|---|
| `BackgroundBrush` | `#E8E4DC` | Fond des fenêtres |
| `FaceBrush` | `#FAFAF7` | Cadran horloge (clair) |
| `TextPrimaryBrush` | `#1C1C1A` | Graduations longues, labels sombres sur cadran clair |
| `TextOnDarkBrush` | `#1C1C1A` | Textes calendrier |
| `HandBrush` | `#1C1C1A` | Aiguilles heures et minutes |
| `AccentBrush` | `#E85D00` | Aiguille secondes |
| `MutedBrush` | `#B0ADA6` | Graduations courtes, textes secondaires |

### Typographie
- Titres / chiffres dominants : **Helvetica Neue** (fallback : Segoe UI Light)
- Corps / grille : Segoe UI Light
- Pas de serif, pas de gras sauf intention forte

### Horloge — détails visuels (Canvas 500×500 logical units, scalé par Viewbox)

| Élément | Constante | Valeur |
|---|---|---|
| Rayon cadran | `FaceR` | 228 |
| Largeur aiguille heures | `HourHandW/H` | 9 × 135 |
| Largeur aiguille minutes | `MinHandW/H` | 6 × 185 |
| Largeur aiguille secondes | `SecHandW/H` | 3 × 210 |
| Dot central | `CenterDotR` | 6 |
| Graduations longues (×12) | — | traits 22 px, épaisseur 2.5 |
| Graduations courtes (×48) | — | traits 10 px, épaisseur 1.2 |

---

## Points d'attention pour la Phase 2

### Filigrane numérique
- Doit être dessiné **sur le Canvas**, derrière les aiguilles (insérer avant `BuildHands()`)
- `TextBlock` avec `Opacity = config.WatermarkOpacity` (lu depuis `App.Current.Config.Clock`)
- Centré sur `(Cx, Cy)`, grande police (ex. 80–100 px logiques sur le canvas 500×500)
- Clé de couleur : `TextPrimaryBrush` avec opacité override, ou couleur dédiée
- Se mettre à jour dans `UpdateHandAngles()` avec `DateTime.Now.ToString("HH:mm:ss")`

### Aiguille secondes fluide
Dans `UpdateHandAngles()` :
```csharp
// Avant (discret — sauts à chaque tick 100 ms)
_secTransform!.Angle = (totalSeconds % 60) / 60.0 * 360.0;

// Après (continu — inclut les millisecondes)
double totalSecondsWithMs = now.Hour * 3600 + now.Minute * 60 + now.Second + now.Millisecond / 1000.0;
_secTransform!.Angle = (totalSecondsWithMs % 60) / 60.0 * 360.0;
```
Note : `totalSeconds` inclut déjà `Millisecond / 1000.0` — vérifier qu'il est utilisé pour toutes les aiguilles.

### DPI Canvas
Le `Canvas` est dans un `Viewbox` — le scaling est automatique. Vérifier sur 2K :
- `StrokeThickness` des ticks : valeurs logiques, pas physiques → OK
- Text rendering dans le filigrane : `TextOptions.TextFormattingMode="Display"` si flou

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
