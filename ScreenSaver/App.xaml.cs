using ScreenSaver.Core;
using ScreenSaver.Models;

namespace ScreenSaver;

public partial class App : System.Windows.Application
{
    private AppController? _controller;

    public static new App Current => (App)System.Windows.Application.Current;
    public AppConfig Config => _configService!.Config;

    private ConfigService? _configService;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _configService = new ConfigService();
        var config = _configService.Config;

        var monitors = new MonitorManager();
        var media    = new MediaInhibitor();
        var theme    = new ThemeService();
        var idle     = new IdleWatcher(media, config.IdleThresholdSeconds);

        theme.Apply(config.Theme);

        _controller = new AppController(_configService, monitors, media, idle, theme);

        // Fire-and-forget — SMTC RequestAsync can block indefinitely on some
        // machines; idle detection must not wait for it.
        _ = media.InitializeAsync();
        idle.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
