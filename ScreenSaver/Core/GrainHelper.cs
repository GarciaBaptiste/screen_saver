using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenSaver.Core;

internal static class GrainHelper
{
    /// <summary>
    /// Returns a tiled ImageBrush of random grayscale noise for a film-grain overlay.
    /// </summary>
    public static ImageBrush CreateBrush(double opacity = 0.015, int size = 256, int seed = 42)
    {
        var rng    = new Random(seed);
        var pixels = new byte[size * size * 4]; // BGRA

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte v = (byte)rng.Next(256);
            pixels[i]     = v;   // B
            pixels[i + 1] = v;   // G
            pixels[i + 2] = v;   // R
            pixels[i + 3] = 255; // A
        }

        var bitmap = BitmapSource.Create(
            size, size, 96, 96,
            PixelFormats.Bgra32, null,
            pixels, size * 4);

        return new ImageBrush(bitmap)
        {
            TileMode     = TileMode.Tile,
            Viewport     = new System.Windows.Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Opacity      = opacity
        };
    }
}
