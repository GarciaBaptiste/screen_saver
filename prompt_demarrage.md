# Prompt de démarrage — ScreenSaver Custom

---

Nous allons développer un économiseur d'écran custom pour Windows 11. Le projet a déjà été planifié en détail : lis `plan.md` et `CLAUDE.md` avant de faire quoi que ce soit d'autre.

## Contexte rapide

- Standalone `.exe` WPF / C# / .NET 8, pas de `.scr` natif
- Deux écrans : principal 2K paysage (horloge analogique Braun) + secondaire 1080p portrait (calendrier)
- Gestion idle in-app via `GetLastInputInfo` + inhibition SMTC pour ne pas se déclencher pendant une vidéo
- Mode single-monitor quand le secondaire est coupé via Win+P
- Style Dieter Rams / Braun, deux thèmes dark/light définis comme ResourceDictionary WPF
- Config via `config.json` à côté de l'exe, pas de registry

## Ce que tu dois faire maintenant — Phase 1 MVP

Lance la Phase 1 dans cet ordre :

1. **Setup solution** : crée `ScreenSaver.sln` + projet WPF `ScreenSaver` en .NET 8. Ajoute le manifest DPI `PerMonitorV2`. Vérifie que la solution compile à vide.

2. **Models** : `MonitorInfo.cs`, `MonitorTopology.cs` (enum `DualMonitor` / `SingleMonitor`), `AppConfig.cs` (sérialisable JSON, correspond au schéma dans CLAUDE.md).

3. **Core — ConfigService** : lit `config.json` depuis le dossier de l'exe, désérialise en `AppConfig`. Si le fichier est absent, écrit un fichier par défaut.

4. **Core — MonitorManager** : P/Invoke `EnumDisplayMonitors` + `GetMonitorInfo`, identifie primary vs secondary, détermine la topologie, expose `PrimaryMonitor`, `SecondaryMonitors`, `CurrentTopology`. Écoute `WM_DISPLAYCHANGE` (via un `HwndSource` sur une fenêtre cachée) et lève un événement `TopologyChanged`.

5. **Core — MediaInhibitor** : utilise `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager` pour détecter si une session est en état `Playing`. Expose `IsMediaPlaying` (bool) mis à jour par un timer à 5 s.

6. **Core — IdleWatcher** : polling `GetLastInputInfo` à 1 s. Suspend le compteur si `MediaInhibitor.IsMediaPlaying`. Lève `IdleStarted` quand le seuil est atteint, `ActivityResumed` à la reprise d'input.

7. **Themes** : `Theme.Dark.xaml` et `Theme.Light.xaml` avec les tokens de couleur définis dans CLAUDE.md. `ThemeService` qui swap le `ResourceDictionary` actif.

8. **Controls — AnalogClock** : UserControl WPF dessiné sur un `Canvas`. Cadran cercle, 60 graduations, 3 aiguilles animées via `DispatcherTimer` à 100 ms avec `RotateTransform`. Pas de filigrane numérique pour l'instant (Phase 2).

9. **Controls — CalendarView** : UserControl avec layout adaptatif. Mode portrait (secondaire actif) : jour semaine + grand numéro + mois/année + grille mensuelle empilés verticalement. Mode paysage compact (single-monitor) : réorganisation horizontale via `VisualState`.

10. **Windows** : `ClockWindow` et `CalendarWindow` — `WindowStyle=None`, `Topmost=true`, `ShowInTaskbar=False`, style Win32 `WS_EX_TOOLWINDOW`. Positionnement via les bounds retournées par `MonitorManager` (pas de `Maximized`). Wake sur `MouseMove` ou `KeyDown`.

11. **AppController** : orchestre tout. Au démarrage écoute `IdleWatcher`. Sur `IdleStarted` : ouvre les fenêtres selon la topologie courante. Sur `ActivityResumed` : ferme toutes les fenêtres. Sur `TopologyChanged` : ferme et reconstruit.

12. **App.xaml.cs** : instancie `AppController`, supprime la `StartupUri`, gère `Application.Exit` proprement.

13. **Script de setup** (`setup.ps1`) : enregistre la tâche dans le Planificateur de tâches Windows — déclencheur : ouverture de session, délai 30 s, chemin vers l'exe publié.

## Contraintes à respecter

- Pas de NuGet externe (sauf accord explicite)
- Positionnement toujours via les bounds `MonitorManager`, jamais `WindowState=Maximized`
- Zéro commentaire inutile dans le code — seulement quand le POURQUOI est non-obvious
- Ne pas implémenter le filigrane numérique, les données calendrier externes, ni l'UI de config — c'est Phase 2+

## Quand la Phase 1 est prête

Dis-moi que c'est compilable et fonctionnel, montre la structure de fichiers créée, et signale tout écart par rapport au plan avec sa justification. On passera ensuite au polish visuel Phase 2.
