using System.Diagnostics;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.WinUI.Models;
using OneOf;
using OneOf.Types;
using Serilog;
using Success = ErrorOr.Success;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class ModSettingsService
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;

    public ModSettingsService(ISkinManagerService skinManagerService, NotificationManager notificationManager,
        ILogger logger)
    {
        _skinManagerService = skinManagerService;
        _notificationManager = notificationManager;
        _logger = logger.ForContext<ModSettingsService>();
    }


    public async Task<OneOf<Success, ModNotFound, Error<Exception>>> SaveSettingsAsync(ModModel modModel)
    {
        var mod = _skinManagerService.GetModById(modModel.Id);

        if (mod is null)
            return new ModNotFound(modModel.Id);


        var modUrl = Uri.TryCreate(modModel.ModUrl, UriKind.Absolute, out var uriResult) &&
                     (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
            ? uriResult
            : null;

        var modSettings = new ModSettings(
            id: modModel.Id,
            customName: EmptyStringToNull(modModel.Name),
            author: EmptyStringToNull(modModel.Author),
            version: EmptyStringToNull(modModel.ModVersion),
            modUrl: modUrl,
            imagePath: modModel.ImagePath == ModModel.PlaceholderImagePath ? null : modModel.ImagePath,
            characterSkinOverride: EmptyStringToNull(modModel.CharacterSkinOverride)
        );


        try
        {
            await Task.Run(() => mod.Settings.SaveSettingsAsync(modSettings));
            return new Success();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to save settings for mod {modName}", mod.Name);

            _notificationManager.ShowNotification($"Failed to save settings for mod {mod.Name}",
                $"An Error Occurred. Reason: {e.Message}",
                TimeSpan.FromSeconds(5));

            return new Error<Exception>(e);
        }
    }

    public async Task<OneOf<Success, NotFound, ModNotFound, Error<Exception>>> SetCharacterSkinOverride(Guid modId,
        string skinName)
    {
        var mod = _skinManagerService.GetModById(modId);

        if (mod is null)
            return new ModNotFound(modId);

        var modSettings = await GetSettingsAsync(modId);

        if (modSettings.TryPickT0(out var settings, out var errorResults))
        {
            var newSettings = settings.DeepCopyWithProperties(newCharacterSkinOverride: skinName);
            try
            {
                await mod.Settings.SaveSettingsAsync(newSettings);
                return new Success();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to save settings for mod {modName}", mod.Name);

                _notificationManager.ShowNotification($"Failed to save settings for mod {mod.Name}",
                    $"An Error Occurred. Reason: {e.Message}",
                    TimeSpan.FromSeconds(5));

                return new Error<Exception>(e);
            }
        }

        if (errorResults.IsT0)
            return errorResults.AsT0;

        if (errorResults.IsT1)
            return errorResults.AsT1;

        return errorResults.AsT2;
    }

    public async Task<OneOf<ModSettings, NotFound, ModNotFound, Error<Exception>>> GetSettingsAsync(Guid modId,
        bool forceReload = false)
    {
        var mod = _skinManagerService.GetModById(modId);

        if (mod is null)
        {
            Debugger.Break();
            _logger.Debug("Could not find mod with id {ModId}", modId);
            return new ModNotFound(modId);
        }

        try
        {
            return await mod.Settings.ReadSettingsAsync();
        }
        catch (ModSettingsNotFoundException e)
        {
            _logger.Error(e, "Could not find settings file for mod {ModName}", mod.Name);
            _notificationManager.ShowNotification($"Could not find settings file for mod {mod.Name}", "",
                TimeSpan.FromSeconds(5));
            return new NotFound();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to read settings for mod {modName}", mod.Name);

            _notificationManager.ShowNotification($"Failed to read settings for mod {mod.Name}",
                $"An error occurred. Reason: {e.Message}",
                TimeSpan.FromSeconds(5));

            return new Error<Exception>(e);
        }
    }


    private static string? EmptyStringToNull(string? str) => string.IsNullOrWhiteSpace(str) ? null : str;
}

public readonly struct ModNotFound
{
    public ModNotFound(Guid modId)
    {
        ModId = modId;
    }

    public Guid ModId { get; }
}