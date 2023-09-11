using Microsoft.UI.Xaml.Data;

namespace GIMI_ModManager.WinUI.Helpers;

public class ValueToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return value;
    }
}