using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenSaver;

namespace ScreenSaver.Controls;

public partial class CalendarView : UserControl
{
    private bool _isCompact;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public CalendarView()
    {
        InitializeComponent();
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _timer.Tick += (_, _) => Refresh();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();
    }

    /// <summary>
    /// Switch between portrait (full secondary screen) and compact landscape (half primary screen).
    /// </summary>
    public bool IsCompact
    {
        get => _isCompact;
        set
        {
            _isCompact = value;
            ApplyLayout();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLayout();
        Refresh();
        _timer.Start();
    }

    private void ApplyLayout()
    {
        PortraitPanel.Visibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
        LandscapePanel.Visibility = _isCompact ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh()
    {
        var today = DateTime.Today;
        var config = App.Current.Config;

        DayOfWeek firstDay = config.Calendar.FirstDayOfWeek == "Sunday"
            ? DayOfWeek.Sunday : DayOfWeek.Monday;

        string dayOfWeek = today.ToString("dddd").ToUpperInvariant();
        string dayNumber = today.Day.ToString();
        string monthYear = today.ToString("MMMM yyyy").ToUpperInvariant();

        // Portrait
        DayOfWeekText.Text = dayOfWeek;
        DayNumberText.Text = dayNumber;
        MonthYearText.Text = monthYear;
        BuildMonthGrid(MonthGrid, today, firstDay, compact: false);

        // Landscape
        DayOfWeekTextL.Text = dayOfWeek;
        DayNumberTextL.Text = dayNumber;
        MonthYearTextL.Text = monthYear;
        BuildMonthGrid(MonthGridL, today, firstDay, compact: true);
    }

    private void BuildMonthGrid(Grid container, DateTime today, DayOfWeek firstDay, bool compact)
    {
        container.Children.Clear();
        container.RowDefinitions.Clear();
        container.ColumnDefinitions.Clear();

        double cellSize = compact ? 28 : 38;
        double fontSize = compact ? 11 : 14;

        for (int c = 0; c < 7; c++)
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });

        // Header row (day names)
        container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });

        var headers = GetDayHeaders(firstDay);
        for (int c = 0; c < 7; c++)
        {
            var hdr = new TextBlock
            {
                Text = headers[c],
                FontSize = fontSize - 1,
                FontFamily = new FontFamily("Segoe UI Light"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            hdr.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            Grid.SetRow(hdr, 0);
            Grid.SetColumn(hdr, c);
            container.Children.Add(hdr);
        }

        // Day cells
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);
        int startOffset = ((int)firstOfMonth.DayOfWeek - (int)firstDay + 7) % 7;
        int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        int totalCells = startOffset + daysInMonth;
        int rowCount = (int)Math.Ceiling(totalCells / 7.0);

        for (int r = 0; r < rowCount; r++)
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });

        for (int d = 1; d <= daysInMonth; d++)
        {
            int cellIndex = startOffset + d - 1;
            int row = cellIndex / 7 + 1;
            int col = cellIndex % 7;

            var date = new DateTime(today.Year, today.Month, d);
            bool isToday = d == today.Day;
            bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

            var cell = new TextBlock
            {
                Text = d.ToString(),
                FontSize = fontSize,
                FontFamily = new FontFamily("Segoe UI Light"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = isToday ? FontWeights.Normal : FontWeights.Light
            };

            if (isToday)
                cell.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            else if (isWeekend)
                cell.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            else
                cell.SetResourceReference(TextBlock.ForegroundProperty, "TextOnDarkBrush");

            if (isWeekend && !isToday)
                cell.Opacity = 0.55;

            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            container.Children.Add(cell);
        }
    }

    private static string[] GetDayHeaders(DayOfWeek firstDay)
    {
        // Indexed by DayOfWeek: Sun=0, Mon=1, Tue=2, Wed=3, Thu=4, Fri=5, Sat=6
        string[] names = ["S", "M", "T", "W", "T", "F", "S"];
        int start = (int)firstDay;
        var result = new string[7];
        for (int i = 0; i < 7; i++)
            result[i] = names[(start + i) % 7];
        return result;
    }
}
