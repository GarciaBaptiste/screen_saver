using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ScreenSaver.Controls;

public partial class AnalogClock : UserControl
{
    // ── Layout constants (logical units on a 500×500 canvas) ────────────────
    private const double Cx = 250, Cy = 250, FaceR = 228;
    private const double HourHandW = 9, HourHandH = 135;
    private const double MinHandW = 6, MinHandH = 185;
    private const double SecHandW = 3, SecHandH = 210;
    private const double CenterDotR = 6;

    private RotateTransform? _hourTransform, _minTransform, _secTransform;
    private readonly DispatcherTimer _timer;

    public AnalogClock()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => UpdateHandAngles();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildFace();
        BuildTicks();
        BuildHands();
        UpdateHandAngles();
        _timer.Start();
    }

    // ── Face ──────────────────────────────────────────────────────────────────

    private void BuildFace()
    {
        var ellipse = new Ellipse { Width = FaceR * 2, Height = FaceR * 2 };
        ellipse.SetResourceReference(Shape.FillProperty, "FaceBrush");
        Canvas.SetLeft(ellipse, Cx - FaceR);
        Canvas.SetTop(ellipse, Cy - FaceR);
        ClockCanvas.Children.Add(ellipse);
    }

    // ── Tick marks ────────────────────────────────────────────────────────────

    private void BuildTicks()
    {
        for (int i = 0; i < 60; i++)
        {
            bool isHour = i % 5 == 0;
            double outerR = FaceR - 4;
            double innerR = isHour ? outerR - 22 : outerR - 10;
            double strokeW = isHour ? 2.5 : 1.2;

            double angleRad = i * 6.0 * Math.PI / 180.0;
            var x1 = Cx + innerR * Math.Sin(angleRad);
            var y1 = Cy - innerR * Math.Cos(angleRad);
            var x2 = Cx + outerR * Math.Sin(angleRad);
            var y2 = Cy - outerR * Math.Cos(angleRad);

            var line = new Line
            {
                X1 = x1, Y1 = y1,
                X2 = x2, Y2 = y2,
                StrokeThickness = strokeW,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            line.SetResourceReference(Shape.StrokeProperty, isHour ? "TextPrimaryBrush" : "MutedBrush");
            ClockCanvas.Children.Add(line);
        }
    }

    // ── Hands ─────────────────────────────────────────────────────────────────

    private void BuildHands()
    {
        _hourTransform = new RotateTransform(0, HourHandW / 2, HourHandH);
        var hourHand = MakeHand(HourHandW, HourHandH, "HandBrush", _hourTransform);
        Canvas.SetLeft(hourHand, Cx - HourHandW / 2);
        Canvas.SetTop(hourHand, Cy - HourHandH);
        ClockCanvas.Children.Add(hourHand);

        _minTransform = new RotateTransform(0, MinHandW / 2, MinHandH);
        var minHand = MakeHand(MinHandW, MinHandH, "HandBrush", _minTransform);
        Canvas.SetLeft(minHand, Cx - MinHandW / 2);
        Canvas.SetTop(minHand, Cy - MinHandH);
        ClockCanvas.Children.Add(minHand);

        _secTransform = new RotateTransform(0, SecHandW / 2, SecHandH);
        var secHand = MakeHand(SecHandW, SecHandH, "AccentBrush", _secTransform);
        Canvas.SetLeft(secHand, Cx - SecHandW / 2);
        Canvas.SetTop(secHand, Cy - SecHandH);
        ClockCanvas.Children.Add(secHand);

        // Center dot (drawn last so it's on top)
        var dot = new Ellipse { Width = CenterDotR * 2, Height = CenterDotR * 2 };
        dot.SetResourceReference(Shape.FillProperty, "AccentBrush");
        Canvas.SetLeft(dot, Cx - CenterDotR);
        Canvas.SetTop(dot, Cy - CenterDotR);
        ClockCanvas.Children.Add(dot);
    }

    private static Rectangle MakeHand(double width, double height, string brushKey, RotateTransform transform)
    {
        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = width / 2,
            RadiusY = width / 2,
            RenderTransform = transform
        };
        rect.SetResourceReference(Shape.FillProperty, brushKey);
        return rect;
    }

    // ── Angle update ──────────────────────────────────────────────────────────

    private void UpdateHandAngles()
    {
        if (_hourTransform is null) return;

        var now = DateTime.Now;
        double totalSeconds = now.Hour * 3600 + now.Minute * 60 + now.Second + now.Millisecond / 1000.0;

        _hourTransform.Angle = totalSeconds / 43200.0 * 360.0;  // 12 h = 43200 s
        _minTransform!.Angle = (totalSeconds % 3600) / 3600.0 * 360.0;
        _secTransform!.Angle = (totalSeconds % 60) / 60.0 * 360.0;
    }
}
