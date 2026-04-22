using System.Drawing;

namespace ScreenSaver.Models;

public sealed class MonitorInfo
{
    public required IntPtr Handle { get; init; }
    public required Rectangle PhysicalBounds { get; init; }
    public required Rectangle WorkArea { get; init; }
    public required bool IsPrimary { get; init; }
    /// <summary>Effective DPI reported by GetDpiForMonitor (MDT_EFFECTIVE_DPI).</summary>
    public required uint DpiX { get; init; }
    public required uint DpiY { get; init; }

    public double DpiScaleX => DpiX / 96.0;
    public double DpiScaleY => DpiY / 96.0;
}
