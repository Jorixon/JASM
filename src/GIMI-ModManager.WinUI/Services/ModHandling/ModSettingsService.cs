using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.Core.Entities.Mods.Helpers;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services.Notifications;
using OneOf;
using OneOf.Types;
using Serilog;
using static GIMI_ModManager.WinUI.Helpers.HandlerServiceHelpers;
using Success = ErrorOr.Success;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class ModSettingsService
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILogger _logger;
    private readonly Notifications.NotificationManager _notificationManager;

    public ModSettingsService(ISkinManagerService skinManagerService,
        Notifications.NotificationManager notificationManager,
        ILogger logger)
    {
        _skinManagerService = skinManagerService;
        _notificationManager = notificationManager;
        _logger = logger.ForContext<ModSettingsService>();
    }


    public async Task<OneOf<Success, ModNotFound, Error<Exception>>> LegacySaveSettingsAsync(ModModel modModel)
    {
        var mod = _skinManagerService.GetModById(modModel.Id);

        if (mod is null)
            return new ModNotFound(modModel.Id);

        if (!mod.Settings.TryGetSettings(out var modSettings))
            return new Error<Exception>(new ModSettingsNotFoundException(mod.FullPath));


        var modUrl = Uri.TryCreate(modModel.ModUrl, UriKind.Absolute, out var uriResult) &&
                     (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
            ? uriResult
            : null;

        modSettings = modSettings.DeepCopyWithProperties(
            customName: NewValue<string?>.Set(EmptyStringToNull(modModel.Name)),
            author: NewValue<string?>.Set(EmptyStringToNull(modModel.Author)),
            modUrl: NewValue<Uri?>.Set(modUrl),
            imagePath: NewValue<Uri?>.Set(modModel.ImagePath == ModModel.PlaceholderImagePath
                ? null
                : modModel.ImagePath),
            characterSkinOverride: NewValue<string?>.Set(EmptyStringToNull(modModel.CharacterSkinOverride))
        );


        try
        {
            await mod.Settings.SaveSettingsAsync(modSettings).ConfigureAwait(false);
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
            var newSettings = settings.DeepCopyWithProperties(characterSkinOverride: NewValue<string?>.Set(skinName));
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
            return await mod.Settings.ReadSettingsAsync().ConfigureAwait(false);
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


    public async Task<Result<ModSettings>> SaveSettingsAsync(Guid modId, UpdateSettingsRequest change, CancellationToken cancellationToken = default)
    {
        return await CommandWrapperAsync(async () =>
        {
            if (!change.AnyUpdates)
                return Result<ModSettings>.Error(new SimpleNotification("No changes were detected", "Mod settings not updated"));

            var mod = _skinManagerService.GetModById(modId);

            if (mod is null)
                throw new ModNotFoundException(modId);

            var oldModSettings = await mod.Settings.TryReadSettingsAsync(cancellationToken: cancellationToken);

            if (oldModSettings is null)
                throw new ModSettingsNotFoundException(mod);

            if (change.ImagePath.HasValue
                && change.ImagePath.Value.ValueToSet != null
                && change.ImagePath.Value.ValueToSet.IsFile
                && !File.Exists(change.ImagePath.Value.ValueToSet.LocalPath)
               )
            {
                change.ImagePath = null;
                _notificationManager.ShowNotification("Image path not found",
                    "When saving mod settings the currently set image could not be found",
                    TimeSpan.FromSeconds(5));
            }


            var newModSettings = oldModSettings.DeepCopyWithProperties(
                author: change.Author.EmptyStringToNull(),
                modUrl: change.ModUrl,
                imagePath: change.ImagePath != null && change.ImagePath.Value == ImageHandlerService.StaticPlaceholderImageUri
                    ? NewValue<Uri?>.Set(null)
                    : change.ImagePath,
                characterSkinOverride: change.CharacterSkinOverride.EmptyStringToNull(),
                customName: change.CustomName.EmptyStringToNull()
            );


            await mod.Settings.SaveSettingsAsync(newModSettings).ConfigureAwait(false);

            _logger.Information("Updated modSettings for mod {ModName} ({ModPath})", mod.GetDisplayName(), mod.FullPath);


            newModSettings = await mod.Settings.TryReadSettingsAsync(useCache: true, cancellationToken: cancellationToken);

            if (newModSettings is null)
                throw new ModSettingsNotFoundException(mod);


            return Result<ModSettings>.Success(newModSettings, new SimpleNotification(
                title: "Mod settings updated",
                message: $"Mod settings have been updated for {mod.GetDisplayName()}",
                null
            ));
        }).ConfigureAwait(false);
    }

    public async Task<Result> SetModIniAsync(Guid modId, string modIni, bool autoDetect = false)
    {
        try
        {
            return await InternalSetModIniAsync(modId, modIni, autoDetect).ConfigureAwait(false);
        }
        catch (Exception e)
        {
#if DEBUG
            throw;
#endif

            _logger.Error(e, "Failed to set mod ini for mod {modId}", modId);
            return Result.Error(e);
        }
    }

    private async Task<Result> InternalSetModIniAsync(Guid modId, string modIni, bool autoDetect = false)
    {
        var mod = _skinManagerService.GetModById(modId);

        if (mod is null)
            return Result.Error(new SimpleNotification("Could not find mod",
                "An error occured trying to set Mod ini, restarting JASM may help"));

        var modSettings = await mod.Settings.TryReadSettingsAsync().ConfigureAwait(false);

        if (modSettings is null)
            return Result.Error(new SimpleNotification("Could not find mod settings",
                "An error occured trying to set Mod ini, restarting JASM may help"));

        ModSettings? newSettings;
        if (modIni.IsNullOrEmpty() && autoDetect)
        {
            newSettings =
                modSettings.DeepCopyWithProperties(mergedIniPath: NewValue<Uri?>.Set(null),
                    ignoreMergedIni: NewValue<bool>.Set(false));
            await mod.Settings.SaveSettingsAsync(newSettings).ConfigureAwait(false);
            await mod.GetModIniPathAsync().ConfigureAwait(false);
            return Result.Success();
        }


        if (modIni.Trim() == string.Empty)
        {
            newSettings =
                modSettings.DeepCopyWithProperties(mergedIniPath: null, ignoreMergedIni: NewValue<bool>.Set(true));

            await mod.Settings.SaveSettingsAsync(newSettings).ConfigureAwait(false);
            return Result.Success();
        }


        var modIniUri = Uri.TryCreate(modIni, UriKind.Absolute, out var uriResult) &&
                        (uriResult.Scheme == Uri.UriSchemeFile)
            ? uriResult
            : null;

        if (modIniUri is null)
            return Result.Error(new SimpleNotification("Invalid Mod ini path",
                "An error occured trying to parse the path to the .ini"));

        if (!File.Exists(modIniUri.LocalPath))
            return Result.Error(new SimpleNotification("Mod ini file does not exist",
                $"Could not find file at {modIniUri.LocalPath}"));

        if (!SkinModHelpers.IsInModFolder(mod, modIniUri))
            return Result.Error(new SimpleNotification("Mod ini file is not in mod folder",
                $"The mod ini file must be in the mod folder. Mod folder: {mod.FullPath}\nIni path: {modIniUri.LocalPath}"));

        newSettings =
            modSettings.DeepCopyWithProperties(mergedIniPath: NewValue<Uri?>.Set(modIniUri),
                ignoreMergedIni: NewValue<bool>.Set(false));

        await mod.Settings.SaveSettingsAsync(newSettings).ConfigureAwait(false);
        await mod.GetModIniPathAsync().ConfigureAwait(false);
        return Result.Success();
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

public class ModNotFoundException(Guid modId) : Exception($"Could not find mod with id {modId}");

public class UpdateSettingsRequest
{
    public bool AnyUpdates =>
        GetType()
            .GetProperties()
            .Where(p => p.CanRead && p.Name != nameof(AnyUpdates))
            .Select(p => p.GetValue(this))
            .Any(value => value is not null);

    public NewValue<string?>? Author { get; set; }

    public string? SetAuthor
    {
        set => Author = NewValue<string?>.Set(value);
    }

    public NewValue<Uri?>? ModUrl { get; set; }

    public Uri? SetModUrl
    {
        set => ModUrl = NewValue<Uri?>.Set(value);
    }

    public NewValue<Uri?>? ImagePath { get; set; }

    public Uri? SetImagePath
    {
        set => ImagePath = NewValue<Uri?>.Set(value);
    }

    public NewValue<string?>? CharacterSkinOverride { get; set; }

    public string? SetCharacterSkinOverride
    {
        set => CharacterSkinOverride = NewValue<string?>.Set(value);
    }

    public NewValue<string?>? CustomName { get; set; }

    public string? SetCustomName
    {
        set => CustomName = NewValue<string?>.Set(value);
    }


    // TODO: Could do later, too big for this PR
    //public List<string> CreateUpdateLogEntries(ModSettings newModSettings)
    //{
    //    var changeEntries = new List<string>();

    //    if (Author is not null && newModSettings.Author != Author.Value)
    //    {

    //    }

    //    return changeEntries;

    //    string CreateLogEntry(string propertyName,string oldValue, string newValue)
    //    {
    //        return $""
    //    }
    //}
}

public record Result : IResult
{
    private Result<Success> _result = Result<Success>.Success(new Success());

    public bool IsSuccess => _result.IsSuccess;
    public bool IsError => _result.IsError;
    public Exception? Exception => _result.Exception;
    public string? ErrorMessage => _result.ErrorMessage;
    public SimpleNotification? Notification => _result.Notification;

    [MemberNotNullWhen(true, nameof(Notification))]
    public bool HasNotification => Notification is not null;

    public static Result Success() => new();

    public static Result Success(SimpleNotification notification)
    {
        var result = new Result
        {
            _result = Result<Success>.Success(new Success(), notification)
        };
        return result;
    }

    public static Result Error(Exception exception)
    {
        var result = new Result
        {
            _result = Result<Success>.Error(exception)
        };
        return result;
    }

    public static Result Error(SimpleNotification notification) => new()
    {
        _result = Result<Success>.Error(notification)
    };
}

public record Result<T> : IResult
{
    public T? Value { get; init; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; private init; }

    public bool IsError { get; init; }

    public Exception? Exception { get; init; }

    private readonly string? _errorMessage;

    public string? ErrorMessage
    {
        get
        {
            if (_errorMessage is null && Exception is not null)
                return Exception.Message;

            return _errorMessage;
        }
        init => _errorMessage = value;
    }

    public SimpleNotification? Notification { get; init; }

    [MemberNotNullWhen(true, nameof(Notification))]
    public bool HasNotification => Notification is not null;

    public static Result<T> Success(T value, SimpleNotification notification) =>
        new()
        {
            Value = value,
            IsSuccess = true,
            Notification = notification
        };

    public static Result<T> Success(T value) =>
        new()
        {
            Value = value,
            IsSuccess = true
        };

    public static Result<T> Error(Exception exception) => new()
    {
        IsError = true,
        Exception = exception,
        Notification = new SimpleNotification("An Error Occurred", exception.Message, TimeSpan.FromSeconds(5))
    };

    public static Result<T> Error(string errorMessage) => new()
    {
        IsError = true,
        ErrorMessage = errorMessage,
        Notification = new SimpleNotification("An Error Occurred", errorMessage, TimeSpan.FromSeconds(5))
    };

    public static Result<T> Error(Exception exception, SimpleNotification? notification)
    {
        return new Result<T>
        {
            IsError = true,
            Exception = exception,
            Notification = notification
        };
    }

    public static Result<T> Error(SimpleNotification notification)
    {
        return new Result<T>
        {
            IsError = true,
            Notification = notification
        };
    }
}

public interface IResult
{
    public bool IsSuccess { get; }
    public bool IsError { get; }
    public Exception? Exception { get; }
    public string? ErrorMessage { get; }
    public SimpleNotification? Notification { get; }
    public bool HasNotification { get; }
}