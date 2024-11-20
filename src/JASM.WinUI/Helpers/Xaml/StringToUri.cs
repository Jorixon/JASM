using Microsoft.UI.Xaml.Data;

namespace GIMI_ModManager.WinUI.Helpers.Xaml;

public class StringToUri : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Uri { IsAbsoluteUri: true } uri)
            return uri;

        if (value is not string str)
            return "";
        Uri.TryCreate(str, UriKind.Absolute, out uri!);
        return uri;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString();
    }
}