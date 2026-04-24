using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ScreenSaver.Core;

public sealed class IdleWatcher : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [DllImport("kernel32.dll")] static extern uint GetTickCount();

    private readonly DispatcherTimer _timer;
    private readonly MediaInhibitor  _media;
    private readonly int             _thresholdMs;
    private bool _isIdle;

    public event EventHandler? IdleStarted;
    public event EventHandler? ActivityResumed;

    public IdleWatcher(MediaInhibitor media, int thresholdSeconds)
    {
        _media       = media;
        _thresholdMs = thresholdSeconds * 1000;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop()  => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        if (_media.IsMediaPlaying)
        {
            if (_isIdle) ForceActivity();
            return;
        }

        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        if (!GetLastInputInfo(ref info)) return;

        int idleMs = (int)(GetTickCount() - info.dwTime);

        if (!_isIdle && idleMs >= _thresholdMs)
        {
            _isIdle = true;
            IdleStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Forces idle state immediately (e.g. F15 hotkey). Raises IdleStarted.</summary>
    public void ForceIdle()
    {
        if (_isIdle) return;
        _isIdle = true;
        IdleStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Signals activity from screensaver windows (mouse/keyboard). Raises ActivityResumed.</summary>
    public void ForceActivity()
    {
        _isIdle = false;
        ActivityResumed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _timer.Stop();
}
