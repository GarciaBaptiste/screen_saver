using ScreenSaver.Core;
using ScreenSaver.Models;
using ScreenSaver.Windows;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using WF = System.Windows.Forms;

namespace ScreenSaver;

public partial class App : Application
{
    private AppController?       _controller;
    private ConfigService?       _configService;
    private ThemeService?        _theme;
    private IdleWatcher?         _idle;
    private WF.NotifyIcon?       _trayIcon;
    private WF.ToolStripMenuItem? _menuToggle;

    public static new App Current => (App)Application.Current;
    public AppConfig Config => _configService!.Config;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _configService = new ConfigService();
        var config = _configService.Config;

        var monitors = new MonitorManager();
        var media    = new MediaInhibitor();
        _theme       = new ThemeService();
        _idle        = new IdleWatcher(media, config.IdleThresholdSeconds);

        _theme.Apply(config.Theme);
        _theme.ApplyAccent(ResolveAccent(config.AccentColor));
        _controller = new AppController(_configService, monitors, media, _idle, _theme);

        _ = media.InitializeAsync();
        _idle.Start();

        InitTrayIcon();
    }

    private void InitTrayIcon()
    {
        _menuToggle       = new WF.ToolStripMenuItem("Activer maintenant");
        _menuToggle.Click += (_, _) => _controller?.Toggle();

        var settingsItem   = new WF.ToolStripMenuItem("Paramètres…");
        settingsItem.Click += (_, _) => OpenSettings();

        var quitItem   = new WF.ToolStripMenuItem("Quitter");
        quitItem.Click += (_, _) => Shutdown();

        var menu = new WF.ContextMenuStrip();
        menu.Items.Add(_menuToggle);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(quitItem);

        // Met à jour le libellé juste avant l'affichage du menu
        menu.Opening += (_, _) =>
            _menuToggle.Text = _controller?.IsActive == true ? "Masquer" : "Activer maintenant";

        _trayIcon = new WF.NotifyIcon
        {
            Text             = "ScreenSaver",
            Icon             = CreateTrayIcon(),
            ContextMenuStrip = menu,
            Visible          = true,
        };
        _trayIcon.MouseClick += (_, e) => { if (e.Button == WF.MouseButtons.Left) OpenSettings(); };
    }

    private void OpenSettings()
    {
        // Si le screensaver est actif il couvre l'écran — on le ferme d'abord
        if (_controller?.IsActive == true)
            _controller.Toggle();

        new SettingsWindow(_configService!, _theme!, _idle!).ShowDialog();
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var white  = new Pen(Color.White, 2f);
        using var accent = new SolidBrush(Color.FromArgb(0xBF, 0x4E, 0x16));

        g.DrawEllipse(white, 2, 2, 27, 27);   // cadran
        g.DrawLine(white, 16, 16, 10, 8);      // aiguille heures
        g.DrawLine(white, 16, 16, 16, 5);      // aiguille minutes
        g.FillEllipse(accent, 13, 13, 6, 6);   // dot central

        return Icon.FromHandle(bmp.GetHicon());
    }

    public static readonly string[] AccentColors = { "#E93F29", "#EEA929", "#6518EA", "#00A745" };
    private static readonly Random  _rng = new();

    public static string ResolveAccent(string value) =>
        value == "random"
            ? AccentColors[_rng.Next(AccentColors.Length)]
            : value;

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _controller?.Dispose();
        base.OnExit(e);
    }
}
