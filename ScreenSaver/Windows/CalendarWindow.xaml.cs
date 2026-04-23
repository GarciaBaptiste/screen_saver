using ScreenSaver.Core;
using ScreenSaver.Native;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace ScreenSaver.Windows;

public partial class CalendarWindow : Window
{
    private System.Drawing.Rectangle _physBounds;
    private readonly Action _onMouseMove;
    private readonly Action _onInput;

    public CalendarWindow(Action onMouseMove, Action onInput, bool isCompact)
    {
        InitializeComponent();
        _onMouseMove = onMouseMove;
        _onInput     = onInput;
        CalView.IsCompact = isCompact;
        GrainOverlay.Fill = GrainHelper.CreateBrush();
        Opacity = 0;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    public void PositionOnMonitor(System.Drawing.Rectangle physBounds)
    {
        _physBounds = physBounds;
        Width  = physBounds.Width;
        Height = physBounds.Height;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        Win32.InitToolWindow(new WindowInteropHelper(this).Handle, _physBounds);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Phase 1 — fenêtre transparente (aucun contenu visible)
        var phase1 = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700)) { EasingFunction = ease };
        phase1.Completed += (_, _) =>
        {
            // Phase 2 — fond + grain + sections calendrier en cascade
            var phase2 = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800)) { EasingFunction = ease };
            BackgroundRect.BeginAnimation(OpacityProperty, phase2);
            GrainOverlay.BeginAnimation(OpacityProperty, phase2);
            CalView.StartReveal();
        };
        BeginAnimation(OpacityProperty, phase1);
    }

    protected override void OnMouseMove (MouseEventArgs       e) { base.OnMouseMove(e);  _onMouseMove(); }
    protected override void OnMouseDown (MouseButtonEventArgs e) { base.OnMouseDown(e);  _onInput(); }
    protected override void OnKeyDown   (KeyEventArgs         e) { base.OnKeyDown(e);    _onInput(); }
}
