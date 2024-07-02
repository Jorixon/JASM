using System.Globalization;
using GIMI_ModManager.Core.Contracts.Services;
using Serilog;
using WinUI3Localizer;

namespace GIMI_ModManager.WinUI.Services;

public class Localizer : ILanguageLocalizer
{
    private readonly ILogger _logger;
    private ILocalizer _localizer = null!;


    public ILanguage CurrentLanguage { get; private set; } = null!;
    public ILanguage FallbackLanguage { get; private set; } = null!;

    private List<ILanguage> _availableLanguages = new();
    public IReadOnlyList<ILanguage> AvailableLanguages => _availableLanguages.AsReadOnly();

    public Localizer(ILogger logger)
    {
        _logger = logger;
    }

    public event EventHandler? LanguageChanged;

    public async Task InitializeAsync()
    {
        var stringsFolderPath = Path.Combine(App.ROOT_DIR, "Strings");

        _localizer = await new LocalizerBuilder()
            .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
            .SetOptions(options => { options.DefaultLanguage = "en-us"; })
            .Build();


        foreach (var availableLanguage in _localizer.GetAvailableLanguages())
        {
            var language = new Language(availableLanguage);
            _availableLanguages.Add(language);
        }

        var ci = CultureInfo.CurrentUICulture.Name.ToLower();

        _logger.Information("Current culture: {ci}", ci);

        await SetLanguage(ci).ConfigureAwait(false);
    }

    private async Task SetLanguage(string languageCode)
    {
        var ci = CultureInfo.GetCultureInfo(languageCode);

        CurrentLanguage = new Language(languageCode);
        FallbackLanguage = new Language("en-us");

        if (_localizer.GetAvailableLanguages().Contains(CurrentLanguage.LanguageCode))
        {
            await _localizer.SetLanguage(CurrentLanguage.LanguageCode);
            _logger.Debug("Set language to {ci}", ci);
        }
        else
        {
            await _localizer.SetLanguage(FallbackLanguage.LanguageCode);
            _logger.Debug("Language {ci} is not available", ci);
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }


    public Task SetLanguageAsync(ILanguage language)
    {
        return SetLanguage(language.LanguageCode);
    }

    public Task SetLanguageAsync(string languageCode)
    {
        return CurrentLanguage.LanguageCode == languageCode ? Task.CompletedTask : SetLanguage(languageCode);
    }

    public string GetLocalizedString(string uid)
    {
        try
        {
            return _localizer.GetLocalizedString(uid);
        }
        catch (Exception)
        {
            _logger.Debug("Uid {uid} not found", uid);
        }

        try
        {
            var langDict = _localizer.GetLanguageDictionaries();
            var fallback = langDict.FirstOrDefault(d => d.Language == FallbackLanguage.LanguageCode);
            if (fallback is null)
                return uid;

            fallback.TryGetItems(uid, out var items);

            return items?.LastOrDefault()?.Value ?? uid;
        }
        catch (Exception)
        {
            _logger.Debug("Uid {uid} not found for fallbackLanguage", uid);
            return uid;
        }
    }

    public string? GetLocalizedStringOrDefault(string uid, string? defaultValue = null)
    {
        try
        {
            var text = _localizer.GetLocalizedString(uid);
            if (string.IsNullOrWhiteSpace(text))
                return defaultValue;
            return text;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }
}