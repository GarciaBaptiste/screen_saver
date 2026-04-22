using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ScreenSaver.Models;

namespace ScreenSaver.Core;

public sealed class MonitorManager : IDisposable
{
    // ── Win32 types ───────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const uint MONITORINFOF_PRIMARY = 0x00000001;
    private const int  MDT_EFFECTIVE_DPI    = 0;
    private const int  WM_DISPLAYCHANGE     = 0x007E;

    // ── State ─────────────────────────────────────────────────────────────────

    private HwndSource? _msgWindow;

    // Kept as a field so GC doesn't collect the delegate between pinning and the P/Invoke call
    private readonly MonitorEnumDelegate _enumCallback;
    private List<MonitorInfo>? _enumAccumulator;

    public IReadOnlyList<MonitorInfo> Monitors { get; private set; } = [];
    public MonitorInfo? PrimaryMonitor => Monitors.FirstOrDefault(m => m.IsPrimary);
    public IReadOnlyList<MonitorInfo> SecondaryMonitors => Monitors.Where(m => !m.IsPrimary).ToList();
    public MonitorTopology CurrentTopology => SecondaryMonitors.Count > 0 ? MonitorTopology.DualMonitor : MonitorTopology.SingleMonitor;

    public event EventHandler? TopologyChanged;

    /// <summary>Exposes the hidden message window HWND for external hotkey registration.</summary>
    public IntPtr MessageWindowHandle => _msgWindow?.Handle ?? IntPtr.Zero;

    public void AddMessageHook(System.Windows.Interop.HwndSourceHook hook) =>
        _msgWindow?.AddHook(hook);

    // ── Init ──────────────────────────────────────────────────────────────────

    public MonitorManager()
    {
        _enumCallback = EnumCallback;
        Refresh();
        CreateMessageWindow();
    }

    // ── Enum ──────────────────────────────────────────────────────────────────

    public void Refresh()
    {
        _enumAccumulator = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, _enumCallback, IntPtr.Zero);
        Monitors = _enumAccumulator;
        _enumAccumulator = null;
    }

    private bool EnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
    {
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
        if (!GetMonitorInfo(hMonitor, ref info))
            return true; // Continue enumeration even on error

        GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY);
        if (dpiX == 0) dpiX = 96;
        if (dpiY == 0) dpiY = 96;

        _enumAccumulator!.Add(new MonitorInfo
        {
            Handle        = hMonitor,
            PhysicalBounds = RectFromRECT(info.rcMonitor),
            WorkArea       = RectFromRECT(info.rcWork),
            IsPrimary      = (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
            DpiX           = dpiX,
            DpiY           = dpiY
        });
        return true;
    }

    // ── WM_DISPLAYCHANGE ──────────────────────────────────────────────────────

    private void CreateMessageWindow()
    {
        // WM_DISPLAYCHANGE is only dispatched to top-level windows, not to HWND_MESSAGE children.
        // WS_POPUP (0x80000000) without WS_VISIBLE = borderless, invisible, top-level.
        var parameters = new HwndSourceParameters("ScreenSaverMonitorWatcher")
        {
            WindowStyle         = unchecked((int)0x80000000), // WS_POPUP, no WS_VISIBLE
            ExtendedWindowStyle = 0x00000080,                 // WS_EX_TOOLWINDOW
            Width     = 1,
            Height    = 1,
            PositionX = -32000,
            PositionY = -32000
        };
        _msgWindow = new HwndSource(parameters);
        _msgWindow.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DISPLAYCHANGE)
        {
            // Snapshot topology before refresh to detect real changes.
            // WM_DISPLAYCHANGE can fire spuriously (DWM, notifications, fullscreen apps)
            // without any actual monitor change — we must not reconstruct windows in that case.
            int  prevCount    = Monitors.Count;
            bool prevHasDual  = SecondaryMonitors.Count > 0;

            Refresh();

            bool changed = Monitors.Count != prevCount
                        || (SecondaryMonitors.Count > 0) != prevHasDual;

            if (changed)
                TopologyChanged?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Rectangle RectFromRECT(RECT r) =>
        new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    public void Dispose() => _msgWindow?.Dispose();
}
