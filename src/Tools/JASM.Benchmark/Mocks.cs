using GIMI_ModManager.Core.Contracts.Services;
using Serilog;
using Serilog.Events;

namespace JASM.Benchmark;

public class MockLogger : ILogger
{
    public void Write(LogEvent logEvent)
    {
    }
}

public class MockLocalizer : ILanguageLocalizer
{
    public event EventHandler? LanguageChanged;

    public Task InitializeAsync()
    {
        CurrentLanguage = new Language("en-us");
        FallbackLanguage = new Language("en-us");
        AvailableLanguages = new List<ILanguage> { CurrentLanguage };
        return Task.CompletedTask;
    }

    public ILanguage CurrentLanguage { get; private set; } = new Language("en-us");
    public ILanguage FallbackLanguage { get; private set; } = new Language("en-us");
    public IReadOnlyList<ILanguage> AvailableLanguages { get; private set; } = new List<ILanguage>();

    public Task SetLanguageAsync(ILanguage language)
    {
        return Task.CompletedTask;
    }

    public Task SetLanguageAsync(string languageCode)
    {
        return Task.CompletedTask;
    }

    public string GetLocalizedString(string uid)
    {
        return uid;
    }

    public string? GetLocalizedStringOrDefault(string uid, string? defaultValue = null,
        bool? useUidAsDefaultValue = null)
    {
        return uid;
    }
}