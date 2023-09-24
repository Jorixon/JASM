using GIMI_ModManager.WinUI.Models.Options;
using Microsoft.UI.Xaml.Data;

namespace GIMI_ModManager.WinUI.Helpers;

public class AttentionTypeToSymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        if (parameter is AttentionType attentionType)
        {
            return GetSymbol(attentionType);
        }

        if (value is not null && value.GetType().IsEnum)
        {
            return GetSymbol((AttentionType) value);
        }

        if (parameter is string enumString)
        {
            if (Enum.TryParse(typeof(AttentionType), enumString, out var result))
            {
                return GetSymbol((AttentionType) result);
            }
        }

        if (value is null)
        {
            return "";
        }

        throw new ArgumentException("Value is not of type AttentionType");
    }


    private static string GetSymbol(AttentionType attentionType)
    {
        return attentionType switch
        {
            AttentionType.Added => "\uEA3B",
            AttentionType.Modified => "\uE8F1",
            AttentionType.UpdateAvailable => "\uE8F1",
            AttentionType.Error => "\uE783",
            AttentionType.None => "",
            _ => throw new ArgumentOutOfRangeException(nameof(attentionType), attentionType, null)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string enumString)
        {
            return Enum.Parse(typeof(AttentionType), enumString);
        }

        if (parameter is AttentionType attentionType)
        {
            return attentionType;
        }

        throw new ArgumentException("Value could not be converted AttentionType");
    }
}