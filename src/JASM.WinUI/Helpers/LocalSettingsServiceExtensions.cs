using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.Helpers;

public static class LocalSettingsServiceExtensions
{
    public static Task<CharacterDetailsSettings> ReadCharacterDetailsSettingsAsync(
        this ILocalSettingsService localSettingsService,
        SettingScope scope = SettingScope.App) =>
        localSettingsService.ReadOrCreateSettingAsync<CharacterDetailsSettings>(CharacterDetailsSettings.Key,
            scope);

    public static Task SaveCharacterDetailsSettingsAsync(this ILocalSettingsService localSettingsService,
        CharacterDetailsSettings settings, SettingScope scope = SettingScope.App) =>
        localSettingsService.SaveSettingAsync(CharacterDetailsSettings.Key, settings, scope);
}