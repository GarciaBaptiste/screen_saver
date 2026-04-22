using Windows.Media.Control;

namespace ScreenSaver.Core;

/// <summary>
/// Polls SMTC every 5 s to detect active media playback.
/// Keeps IdleWatcher from triggering while a browser/media-player is Playing.
/// </summary>
public sealed class MediaInhibitor : IDisposable
{
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public bool IsMediaPlaying { get; private set; }

    public MediaInhibitor()
    {
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
            // SMTC unavailable — silently disable inhibition
        }
        _timer.Start();
    }

    private async Task PollAsync()
    {
        if (_manager is null)
        {
            IsMediaPlaying = false;
            return;
        }

        try
        {
            // Re-request in case sessions changed (cheap call, cached by OS)
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = _manager.GetSessions();
            IsMediaPlaying = sessions.Any(s =>
                s.GetPlaybackInfo()?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
        }
        catch
        {
            IsMediaPlaying = false;
        }
    }

    public void Dispose() => _timer.Stop();
}
