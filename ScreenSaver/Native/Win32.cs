using System.Runtime.InteropServices;

namespace ScreenSaver.Native;

/// <summary>
/// Centralized P/Invoke declarations and Win32 constants shared across the project.
/// </summary>
internal static class Win32
{
    // ── Window style constants ────────────────────────────────────────────────

    public const int  GWL_EXSTYLE      = -20;
    public const int  WS_EX_TOOLWINDOW = 0x00000080;
    public const uint SWP_NOACTIVATE   = 0x0010;
    public const uint SWP_NOZORDER     = 0x0004;

    // ── Message constants ─────────────────────────────────────────────────────

    public const int WM_HOTKEY = 0x0312;

    // ── Imports ───────────────────────────────────────────────────────────────

    [DllImport("user32.dll")] public static extern int  GetWindowLong (IntPtr hwnd, int nIndex);
    [DllImport("user32.dll")] public static extern int  SetWindowLong (IntPtr hwnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] public static extern bool SetWindowPos  (IntPtr hwnd, IntPtr hwndAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey  (IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies WS_EX_TOOLWINDOW (removes from Alt+Tab) and positions the window
    /// at its physical pixel bounds via SetWindowPos.
    /// </summary>
    public static void InitToolWindow(IntPtr hwnd, System.Drawing.Rectangle physBounds)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
        SetWindowPos(hwnd, IntPtr.Zero,
            physBounds.Left, physBounds.Top,
            physBounds.Width, physBounds.Height,
            SWP_NOACTIVATE | SWP_NOZORDER);
    }
}
