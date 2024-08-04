using Windows.Foundation;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class ScreenSizeSettings
{
    public ScreenSizeSettings()
    {
    }

    public ScreenSizeSettings(double width, double height)
    {
        Width = Convert.ToInt32(width);
        Height = Convert.ToInt32(height);
    }

    [JsonIgnore] public const string Key = "ScreenSize";

    public bool PersistWindowSize { get; set; } = true;

    public int Width { get; set; }
    public int Height { get; set; }

    public bool PersistWindowPosition { get; set; } = true;
    public int XPosition { get; set; }
    public int YPosition { get; set; }
    public bool IsFullScreen { get; set; }
    [JsonIgnore] public double WidthAsDouble => Convert.ToDouble(Width);
    [JsonIgnore] public double HeightAsDouble => Convert.ToDouble(Height);
    [JsonIgnore] public Size Size => new(WidthAsDouble, HeightAsDouble);
}