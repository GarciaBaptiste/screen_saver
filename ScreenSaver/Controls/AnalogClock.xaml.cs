using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ScreenSaver.Controls;

public partial class AnalogClock : UserControl
{
    // ── Layout constants (logical units on a 500×500 canvas) ────────────────
    private const double Cx = 250, Cy = 250, FaceR = 228;
    private const double HourHandW = 12, HourHandH = 110;
    private const double MinHandW = 8,  MinHandH = 155;
    private const double SecHandW = 3,  SecHandH = FaceR - 1;
    private const double CenterDotR = 14;

    private RotateTransform? _hourTransform, _minTransform, _secTransform, _secCoverTransform;
    // 4 digit cells arranged vertically: HH / MM
    private readonly TextBlock?[] _wmDigits = new TextBlock?[4];
    private static readonly int[] WmCol = [0, 1, 0, 1]; // column per digit
    private static readonly int[] WmRow = [0, 0, 1, 1]; // row per digit
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
        BuildTicks();
        BuildWatermark();
        BuildHands();
        UpdateHandAngles();
        _timer.Start();
    }

    // ── Tick marks ────────────────────────────────────────────────────────────
    // Relief emboss: two offset strokes per mark (no base colour = background shows through).
    // Light source at 315° (upper-left) → highlight offset (-off,-off), shadow offset (+off,+off).

    private static readonly Brush TickHighlight = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
    private static readonly Brush TickShadow    = new SolidColorBrush(Color.FromArgb(85,   0,   0,   0));

    private void BuildTicks()
    {
        for (int i = 0; i < 60; i++)
        {
            bool isHour = i % 5 == 0;
            double outerR  = FaceR - 4;
            double innerR  = isHour ? outerR - 26 : outerR - 13;
            double strokeW = isHour ? 4.5 : 2.8;
            // offset ≈ strokeW/2 so the blur peaks exactly at the mark edge
            double off     = isHour ? 2.0 : 1.2;

            double angleRad = i * 6.0 * Math.PI / 180.0;
            double x1 = Cx + innerR * Math.Sin(angleRad);
            double y1 = Cy - innerR * Math.Cos(angleRad);
            double x2 = Cx + outerR * Math.Sin(angleRad);
            double y2 = Cy - outerR * Math.Cos(angleRad);

            AddTickLine(x1 - off, y1 - off, x2 - off, y2 - off, strokeW, TickHighlight, isHour ? 6.0 : 4.0);
            AddTickLine(x1 + off, y1 + off, x2 + off, y2 + off, strokeW, TickShadow,    isHour ? 8.0 : 5.5);

            if (isHour)
            {
                // Gros marqueurs : contour blanc + remplissage fond
                var outline = new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    StrokeThickness = strokeW,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                outline.SetResourceReference(Shape.StrokeProperty, "ClockWhiteBrush");
                ClockCanvas.Children.Add(outline);

                var fill = new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    StrokeThickness = strokeW - 1.4,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                fill.SetResourceReference(Shape.StrokeProperty, "BackgroundBrush");
                ClockCanvas.Children.Add(fill);
            }
            else
            {
                // Petits marqueurs : surface fond pour masquer le centre, seules les franges emboss restent
                var surface = new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    StrokeThickness = strokeW,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                surface.SetResourceReference(Shape.StrokeProperty, "BackgroundBrush");
                ClockCanvas.Children.Add(surface);
            }
        }
    }

    private void AddTickLine(double x1, double y1, double x2, double y2, double strokeW, Brush brush, double blurRadius)
    {
        ClockCanvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = brush,
            StrokeThickness = strokeW,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Effect = new BlurEffect { Radius = blurRadius, KernelType = KernelType.Gaussian }
        });
    }

    // ── Digital watermark: [H1][H2][ ][M1][M2][ ][S1][S2] ───────────────────
    // Rendered on OverlayCanvas (outside Viewbox) so text rasterises at native
    // screen resolution — no Viewbox stretch, no blurriness.

    // Logical coords in the 500-unit canvas space (used to compute screen px)
    private const double WmCellWL = 14, WmFontSizeL = 18, WmRowHL = 22;
    private const double WmY0L = 90;                              // below 12 o'clock tick
    private const double WmX0L = Cx - WmCellWL;                  // 236 — centres 2 cells

    private void BuildWatermark()
    {
        var config = App.Current.Config.Clock;
        if (!config.ShowDigitalWatermark) return;

        for (int i = 0; i < 4; i++)
        {
            var tb = new TextBlock
            {
                FontFamily = new FontFamily("Helvetica Neue, Segoe UI"),
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                UseLayoutRounding = true
            };
            TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
            tb.SetResourceReference(TextBlock.ForegroundProperty, "ClockWhiteBrush");
            OverlayCanvas.Children.Add(tb);
            _wmDigits[i] = tb;
        }

        SizeChanged += (_, _) => PositionWatermark();
        PositionWatermark();
    }

    private void PositionWatermark()
    {
        if (_wmDigits[0] is null || ActualWidth <= 0) return;

        double scale = Math.Min(ActualWidth, ActualHeight) / 500.0;
        double offX  = (ActualWidth  - 500 * scale) / 2;
        double offY  = (ActualHeight - 500 * scale) / 2;
        double cellW = WmCellWL * scale;
        double fontSize = WmFontSizeL * scale;
        double x0 = offX + WmX0L * scale;
        double y0 = offY + WmY0L * scale;

        double rowH = WmRowHL * scale;

        for (int i = 0; i < 4; i++)
        {
            if (_wmDigits[i] is null) continue;
            _wmDigits[i]!.FontSize = fontSize;
            _wmDigits[i]!.Width    = cellW;
            Canvas.SetLeft(_wmDigits[i]!, x0 + WmCol[i] * cellW);
            Canvas.SetTop(_wmDigits[i]!,  y0 + WmRow[i] * rowH);
        }
    }

    // ── Hands ─────────────────────────────────────────────────────────────────

    private void BuildHands()
    {
        _hourTransform = new RotateTransform(0, HourHandW / 2, HourHandH);
        var hourHand = MakeHand(HourHandW, HourHandH, "ClockWhiteBrush", _hourTransform);
        Canvas.SetLeft(hourHand, Cx - HourHandW / 2);
        Canvas.SetTop(hourHand, Cy - HourHandH);
        ClockCanvas.Children.Add(hourHand);

        _minTransform = new RotateTransform(0, MinHandW / 2, MinHandH);
        var minHand = MakeHand(MinHandW, MinHandH, "ClockWhiteBrush", _minTransform);
        Canvas.SetLeft(minHand, Cx - MinHandW / 2);
        Canvas.SetTop(minHand, Cy - MinHandH);
        ClockCanvas.Children.Add(minHand);

        _secTransform = new RotateTransform(0, SecHandW / 2, SecHandH);
        var secHand = MakeHand(SecHandW, SecHandH, "AccentBrush", _secTransform, shadowOpacity: 0.80, shadowBlur: 10);
        Canvas.SetLeft(secHand, Cx - SecHandW / 2);
        Canvas.SetTop(secHand, Cy - SecHandH);
        ClockCanvas.Children.Add(secHand);

        // Center dot
        var dot = new Ellipse
        {
            Width = CenterDotR * 2, Height = CenterDotR * 2,
            Stroke = HandOutline,
            StrokeThickness = 0.75,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 14, ShadowDepth = 1.5,
                Direction = 315, Opacity = 0.45
            }
        };
        dot.SetResourceReference(Shape.FillProperty, "AccentBrush");
        Canvas.SetLeft(dot, Cx - CenterDotR);
        Canvas.SetTop(dot, Cy - CenterDotR);
        ClockCanvas.Children.Add(dot);

        // Seconds cover — no stroke, topmost layer, hides the dot/hand seam
        _secCoverTransform = new RotateTransform(0, SecHandW / 2, SecHandH);
        var secCover = new Rectangle
        {
            Width = SecHandW, Height = SecHandH,
            RadiusX = SecHandW / 2, RadiusY = SecHandW / 2,
            RenderTransform = _secCoverTransform
        };
        secCover.SetResourceReference(Shape.FillProperty, "AccentBrush");
        Canvas.SetLeft(secCover, Cx - SecHandW / 2);
        Canvas.SetTop(secCover, Cy - SecHandH);
        ClockCanvas.Children.Add(secCover);
    }

    private static readonly Brush HandOutline = new SolidColorBrush(Color.FromArgb(55, 0, 0, 0));

    private static Rectangle MakeHand(double width, double height, string brushKey, RotateTransform transform,
        double shadowOpacity = 0.62, double shadowBlur = 14)
    {
        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = width / 2,
            RadiusY = width / 2,
            Stroke = HandOutline,
            StrokeThickness = 0.75,
            RenderTransform = transform,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = shadowBlur, ShadowDepth = 1.5,
                Direction = 315, Opacity = shadowOpacity
            }
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
        _secTransform!.Angle      = (totalSeconds % 60) / 60.0 * 360.0;
        _secCoverTransform!.Angle = _secTransform.Angle;

        if (_wmDigits[0] is not null)
        {
            string hh = now.ToString("HH"), mm = now.ToString("mm");
            _wmDigits[0]!.Text = hh[0].ToString();
            _wmDigits[1]!.Text = hh[1].ToString();
            _wmDigits[2]!.Text = mm[0].ToString();
            _wmDigits[3]!.Text = mm[1].ToString();
        }
    }
}
