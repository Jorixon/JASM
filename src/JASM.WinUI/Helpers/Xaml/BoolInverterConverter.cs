using Microsoft.UI.Xaml.Data;

namespace GIMI_ModManager.WinUI.Helpers.Xaml;

public class BoolInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool @bool)
            return !@bool;

        throw new ArgumentException("Value must be a boolean", nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool @bool)
            return !@bool;

        throw new ArgumentException("Value must be a boolean", nameof(value));
    }
}