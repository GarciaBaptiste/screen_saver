using Windows.Media.Control;

namespace ScreenSaver.Core;

/// <summary>
/// Polls SMTC every 5 s to detect active video playback.
/// Keeps IdleWatcher from triggering while a browser or video player is Playing.
/// Audio-only apps (Apple Music, Spotify, etc.) are excluded even if they report
/// PlaybackType.Video for animated album art or canvas.
/// </summary>
public sealed class MediaInhibitor : IDisposable
{
    // Apps that report PlaybackType.Video even for audio tracks.
    // Exclude them so they never inhibit the screensaver.
    private static readonly string[] _audioAppKeywords =
        ["AppleMusic", "iTunes", "Spotify", "Tidal", "Deezer", "AmazonMusic"];

    private static bool IsKnownAudioApp(string sourceAppId) =>
        _audioAppKeywords.Any(k => sourceAppId.Contains(k, StringComparison.OrdinalIgnoreCase));

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
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = _manager.GetSessions()
                .Where(s => s.GetPlaybackInfo()?.PlaybackStatus ==
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .ToList();

            bool videoPlaying = false;
            foreach (var session in sessions)
            {
                var sourceId = session.SourceAppUserModelId ?? "";
                if (IsKnownAudioApp(sourceId)) continue;

                try
                {
                    var props = await session.TryGetMediaPropertiesAsync();
                    if (props?.PlaybackType == global::Windows.Media.MediaPlaybackType.Video)
                    {
                        videoPlaying = true;
                        break;
                    }
                }
                catch { /* session may have disappeared */ }
            }
            IsMediaPlaying = videoPlaying;
        }
        catch
        {
            IsMediaPlaying = false;
        }
    }

    public void Dispose() => _timer.Stop();
}
