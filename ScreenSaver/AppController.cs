using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenSaver.Core;
using ScreenSaver.Models;
using ScreenSaver.Native;
using ScreenSaver.Windows;

namespace ScreenSaver;

public sealed class AppController : IDisposable
{
    // ── Manual-trigger hotkey : Ctrl+Alt+S ───────────────────────────────────

    private const int  HOTKEY_MANUAL  = 1;
    private const uint MOD_ALT        = 0x0001;
    private const uint MOD_CONTROL    = 0x0002;
    private const uint VK_S           = 0x53;

    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly ConfigService  _config;
    private readonly MonitorManager _monitors;
    private readonly MediaInhibitor _media;
    private readonly IdleWatcher    _idle;
    private readonly ThemeService   _theme;

    // ── State ─────────────────────────────────────────────────────────────────

    private ClockWindow?    _clockWindow;
    private CalendarWindow? _calendarWindow;
    private System.Windows.Shapes.Rectangle? _debugDot;  // DEBUG — retire en production

    // WPF fires a synthetic MouseMove when a window appears under the cursor.
    // MouseMove wake is disabled for 600 ms after opening; click/key are always immediate.
    private bool             _mouseMoveWakeEnabled;
    private DispatcherTimer? _graceTimer;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AppController(ConfigService config, MonitorManager monitors,
                         MediaInhibitor media, IdleWatcher idle, ThemeService theme)
    {
        _config   = config;
        _monitors = monitors;
        _media    = media;
        _idle     = idle;
        _theme    = theme;

        _idle.IdleStarted         += OnIdleStarted;
        _idle.ActivityResumed     += OnActivityResumed;
        _monitors.TopologyChanged += OnTopologyChanged;

        RegisterHotkey();
        CreateDebugOverlay();
    }

    // ── Debug overlay ─────────────────────────────────────────────────────────

    private void CreateDebugOverlay()
    {
        _debugDot = new System.Windows.Shapes.Rectangle { Width = 18, Height = 18, Fill = Brushes.Green };

        var win = new Window
        {
            WindowStyle      = WindowStyle.None,
            ResizeMode       = ResizeMode.NoResize,
            Topmost          = true,
            ShowInTaskbar    = false,
            AllowsTransparency = false,
            Width  = 18, Height = 18,
            Left   = 4,  Top    = 4,
            Content = _debugDot
        };
        win.Show();

        // Vert = timer en cours (compte l'inactivité)
        // Rouge = timer arrêté (économiseur actif, ou remis à zéro)
        _idle.TimerRunningChanged += (_, isRunning) =>
            _debugDot.Fill = isRunning ? Brushes.Green : Brushes.Red;
    }

    // ── F15 hotkey ────────────────────────────────────────────────────────────

    private void RegisterHotkey()
    {
        var hwnd = _monitors.MessageWindowHandle;
        if (hwnd == IntPtr.Zero) return;

        Win32.RegisterHotKey(hwnd, HOTKEY_MANUAL, MOD_CONTROL | MOD_ALT, VK_S);
        _monitors.AddMessageHook(OnHotkeyMessage);
    }

    private IntPtr OnHotkeyMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != Win32.WM_HOTKEY || wParam.ToInt32() != HOTKEY_MANUAL) return IntPtr.Zero;

        if (_clockWindow is null) _idle.ForceIdle();
        else                      _idle.ForceActivity();

        handled = true;
        return IntPtr.Zero;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnIdleStarted    (object? sender, EventArgs e) => OpenWindows();
    private void OnActivityResumed(object? sender, EventArgs e) => CloseWindows();

    private void OnTopologyChanged(object? sender, EventArgs e)
    {
        if (_clockWindow is not null) { CloseWindows(); OpenWindows(); }
    }

    // ── Open ──────────────────────────────────────────────────────────────────

    private void OpenWindows()
    {
        if (_clockWindow is not null) return;

        _idle.Stop();
        _mouseMoveWakeEnabled = false;

        // Allow mouse-move to wake after 600 ms — long enough to skip the
        // synthetic MouseMove that WPF fires when a window appears under the cursor.
        _graceTimer?.Stop();
        _graceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _graceTimer.Tick += (_, _) => { _graceTimer!.Stop(); _mouseMoveWakeEnabled = true; };
        _graceTimer.Start();

        var primary = _monitors.PrimaryMonitor;
        if (primary is null) return;

        if (_monitors.CurrentTopology == MonitorTopology.DualMonitor)
            OpenDual(primary);
        else
            OpenSingle(primary);
    }

    private void OpenDual(MonitorInfo primary)
    {
        _clockWindow = new ClockWindow(onMouseMove: OnWakeFromMove, onInput: OnWakeFromInput);
        _clockWindow.PositionOnMonitor(primary.PhysicalBounds);

        var secondary = _monitors.SecondaryMonitors.FirstOrDefault();
        if (secondary is not null)
        {
            _calendarWindow = new CalendarWindow(onMouseMove: OnWakeFromMove, onInput: OnWakeFromInput, isCompact: false);
            _calendarWindow.PositionOnMonitor(secondary.PhysicalBounds);
        }

        ScheduleEntrance();
        _clockWindow.Show();
        _calendarWindow?.Show();
    }

    private void OpenSingle(MonitorInfo primary)
    {
        var b     = primary.PhysicalBounds;
        int halfW = b.Width / 2;

        _clockWindow = new ClockWindow(onMouseMove: OnWakeFromMove, onInput: OnWakeFromInput);
        _clockWindow.PositionOnMonitor(new Rectangle(b.Left, b.Top, halfW, b.Height));

        _calendarWindow = new CalendarWindow(onMouseMove: OnWakeFromMove, onInput: OnWakeFromInput, isCompact: true);
        _calendarWindow.PositionOnMonitor(new Rectangle(b.Left + halfW, b.Top, halfW, b.Height));

        ScheduleEntrance();
        _clockWindow.Show();
        _calendarWindow.Show();
    }

    /// <summary>
    /// Attend que toutes les fenêtres soient chargées (événement Loaded), puis
    /// déclenche BeginEntrance() sur toutes dans le même appel — garantit que
    /// les animations de fond démarrent exactement au même moment.
    /// </summary>
    private void ScheduleEntrance()
    {
        int total  = _calendarWindow is not null ? 2 : 1;
        int loaded = 0;

        void OnLoaded(object? s, System.Windows.RoutedEventArgs e)
        {
            if (++loaded < total) return;
            // Les deux fenêtres sont prêtes : on démarre les deux animations
            // dans le même appel pour une synchronisation au tick près.
            _clockWindow!.BeginEntrance();
            _calendarWindow?.BeginEntrance();
        }

        _clockWindow!.Loaded    += OnLoaded;
        if (_calendarWindow is not null)
            _calendarWindow.Loaded += OnLoaded;
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    private void CloseWindows()
    {
        _graceTimer?.Stop();
        _graceTimer = null;
        _mouseMoveWakeEnabled = false;

        _clockWindow?.Close();    _clockWindow    = null;
        _calendarWindow?.Close(); _calendarWindow = null;

        _idle.Start();
    }

    // ── Wake ──────────────────────────────────────────────────────────────────

    private void OnWakeFromMove()
    {
        if (!_mouseMoveWakeEnabled) return;
        _idle.ForceActivity();
    }

    private void OnWakeFromInput() => _idle.ForceActivity();

    // ── Theme ─────────────────────────────────────────────────────────────────

    public void ApplyTheme(string theme) => _theme.Apply(theme);

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        var hwnd = _monitors.MessageWindowHandle;
        if (hwnd != IntPtr.Zero)
            Win32.UnregisterHotKey(hwnd, HOTKEY_MANUAL);

        _idle.IdleStarted         -= OnIdleStarted;
        _idle.ActivityResumed     -= OnActivityResumed;
        _monitors.TopologyChanged -= OnTopologyChanged;

        _graceTimer?.Stop();
        _clockWindow?.Close();
        _calendarWindow?.Close();
        _idle.Dispose();
        _media.Dispose();
        _monitors.Dispose();
    }
}
