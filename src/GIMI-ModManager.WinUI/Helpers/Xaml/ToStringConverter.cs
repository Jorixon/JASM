using Microsoft.UI.Xaml.Data;

namespace GIMI_ModManager.WinUI.Helpers.Xaml;

public class ToStringConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString();
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString();
    }
}