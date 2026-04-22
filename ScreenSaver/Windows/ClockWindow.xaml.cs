using ScreenSaver.Native;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenSaver.Windows;

public partial class ClockWindow : Window
{
    private System.Drawing.Rectangle _physBounds;
    private readonly Action _onMouseMove;
    private readonly Action _onInput;

    public ClockWindow(Action onMouseMove, Action onInput)
    {
        InitializeComponent();
        _onMouseMove = onMouseMove;
        _onInput     = onInput;
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
