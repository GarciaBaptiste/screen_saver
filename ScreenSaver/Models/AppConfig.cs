using System.Text.Json.Serialization;

namespace ScreenSaver.Models;

public sealed class AppConfig
{
    [JsonPropertyName("idle_threshold_seconds")]
    public int IdleThresholdSeconds { get; set; } = 120;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "dark";

    [JsonPropertyName("accent_color")]
    public string AccentColor { get; set; } = "#E93F29";

    [JsonPropertyName("clock")]
    public ClockConfig Clock { get; set; } = new();

    [JsonPropertyName("calendar")]
    public CalendarConfig Calendar { get; set; } = new();
}

public sealed class ClockConfig
{
    [JsonPropertyName("show_digital_watermark")]
    public bool ShowDigitalWatermark { get; set; } = true;

    [JsonPropertyName("watermark_opacity")]
    public double WatermarkOpacity { get; set; } = 0.12;

    [JsonPropertyName("font_family")]
    public string FontFamily { get; set; } = "Segoe UI Light";
}

public sealed class CalendarConfig
{
    [JsonPropertyName("show_month_grid")]
    public bool ShowMonthGrid { get; set; } = true;

    [JsonPropertyName("first_day_of_week")]
    public string FirstDayOfWeek { get; set; } = "Monday";
}
