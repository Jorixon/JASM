using System.Globalization;

namespace GIMI_ModManager.Core.Contracts.Services;

public interface ILanguageLocalizer
{
    public Task InitializeAsync();
    public ILanguage CurrentLanguage { get; }
    public ILanguage FallbackLanguage { get; }

    public IReadOnlyList<ILanguage> AvailableLanguages { get; }

    public Task SetLanguageAsync(ILanguage language);
    public Task SetLanguageAsync(string languageCode);

    public string GetLocalizedString(string uid);
}

public interface ILanguage : IEquatable<ILanguage>, IEquatable<CultureInfo>
{
    public string LanguageCode { get; }
    public string LanguageName { get; }
}

public class Language : ILanguage
{
    public string LanguageCode { get; }

    public string LanguageName { get; }

    public Language(string languageCode)
    {
        LanguageCode = languageCode.Trim().ToLower();
        try
        {
            LanguageName = CultureInfo.GetCultureInfo(LanguageCode).NativeName;
        }
        catch (CultureNotFoundException)
        {
            LanguageName = LanguageCode;
        }
    }

    public bool Equals(ILanguage? other)
    {
        return other != null && LanguageCode.Equals(other.LanguageCode, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(CultureInfo? other)
    {
        return other != null && LanguageCode.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;

        return obj is ILanguage other && Equals(other) ||
               obj is CultureInfo otherCultureInfo && Equals(otherCultureInfo);
    }

    public override int GetHashCode()
    {
        return LanguageCode.GetHashCode();
    }

    public override string ToString()
    {
        return $"{LanguageName} ({LanguageCode})";
    }
}