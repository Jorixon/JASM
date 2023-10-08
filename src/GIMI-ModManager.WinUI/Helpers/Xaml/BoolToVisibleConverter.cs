using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GIMI_ModManager.WinUI.Helpers.Xaml;

internal class BoolToVisibleConverter : IValueConverter
{
    // https://stackoverflow.com/questions/51542728/uwp-boolean-to-visibility-converter-doesnt-work
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool && (bool)value)
            return Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var visibility = value as Visibility?;
        return visibility != null && visibility == Visibility.Visible;
    }
}