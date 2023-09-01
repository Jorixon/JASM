using Windows.Foundation;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class ScreenSize
{
    public ScreenSize()
    {
    }

    public ScreenSize(double width, double height)
    {
        Width = Convert.ToInt32(width);
        Height = Convert.ToInt32(height);
    }

    [JsonIgnore] public const string Key = "ScreenSize";

    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsFullScreen { get; set; }
    [JsonIgnore] public double WidthAsDouble => Convert.ToDouble(Width);
    [JsonIgnore] public double HeightAsDouble => Convert.ToDouble(Height);
    [JsonIgnore] public Size Size => new Size(WidthAsDouble, HeightAsDouble);
}