using ScreenSaver.Core;
using ScreenSaver.Native;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

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
        SourceInitialized += OnSourceInitialized;
    }

    public void PositionOnMonitor(System.Drawing.Rectangle physBounds)
    {
        _physBounds = physBounds;
        Width  = physBounds.Width;
        Height = physBounds.Height;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        Win32.InitToolWindow(new WindowInteropHelper(this).Handle, _physBounds);

    protected override void OnMouseMove (MouseEventArgs       e) { base.OnMouseMove(e);  _onMouseMove(); }
    protected override void OnMouseDown (MouseButtonEventArgs e) { base.OnMouseDown(e);  _onInput(); }
    protected override void OnKeyDown   (KeyEventArgs         e) { base.OnKeyDown(e);    _onInput(); }
}
