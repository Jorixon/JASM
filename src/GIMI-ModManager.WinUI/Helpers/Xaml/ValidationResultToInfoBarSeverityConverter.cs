using GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace GIMI_ModManager.WinUI.Helpers.Xaml;

public class ValidationResultToInfoBarSeverityConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ValidationType type)
            return null;

        return type switch
        {
            ValidationType.Information => InfoBarSeverity.Informational,
            ValidationType.Warning => InfoBarSeverity.Warning,
            ValidationType.Error => InfoBarSeverity.Error,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is not InfoBarSeverity severity)
            return null;

        return severity switch
        {
            InfoBarSeverity.Informational => ValidationType.Information,
            InfoBarSeverity.Warning => ValidationType.Warning,
            InfoBarSeverity.Error => ValidationType.Error,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}