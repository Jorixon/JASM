using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.Core.Entities.Mods.FileModels;
using GIMI_ModManager.Core.Entities.Mods.Helpers;
using GIMI_ModManager.Core.Helpers;
using Newtonsoft.Json;
using OneOf;
using Serilog;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GIMI_ModManager.Core.Entities.Mods.SkinMod;

public class SkinModSettingsManager
{
    private readonly ISkinMod _skinMod;
    private readonly IReadOnlyCollection<string> _supportedImageExtensions = Constants.SupportedImageExtensions;
    private readonly string configFileName = Constants.ModConfigFileName;
    private const string ImageName = ".JASM_Cover";

    private string _settingsFilePath => Path.Combine(_skinMod.FullPath, configFileName);

    private ModSettings? _settings;

    public bool HasMergedIni => _settings?.MergedIniPath is not null;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };


    internal SkinModSettingsManager(ISkinMod skinMod)
    {
        _skinMod = skinMod;
    }


    internal async Task<Guid> InitializeAsync()
    {
        // Check if the settings file exists

        if (File.Exists(_settingsFilePath))
        {
            var modSettings = await ReadSettingsAsync().ConfigureAwait(false);
            var updateSettings = false;

            if (modSettings.Id == Guid.Empty)
            {
                modSettings.Id = Guid.NewGuid();
                updateSettings = true;
            }

            if (modSettings.DateAdded is null)
            {
                modSettings.DateAdded = DateTime.Now;
                updateSettings = true;
            }

            if (modSettings.ImagePath is not null && !File.Exists(modSettings.ImagePath.LocalPath))
            {
                modSettings.ImagePath = null;
                updateSettings = true;
            }

            if (modSettings.ImagePath is null)
            {
                var images = SkinModHelpers.DetectModPreviewImages(_skinMod.FullPath);
                if (images.Any())
                {
                    modSettings.ImagePath = images.FirstOrDefault();
                    updateSettings = true;
                }
            }


            if (updateSettings)
                await SaveSettingsAsync(modSettings).ConfigureAwait(false);

            return modSettings.Id;
        }

        var newId = Guid.NewGuid();

        var image = SkinModHelpers.DetectModPreviewImages(_skinMod.FullPath).FirstOrDefault();

        var settings = new JsonModSettings()
        {
            Id = newId.ToString(),
            ImagePath = image?.LocalPath,
            DateAdded = DateTime.Now.ToString(CultureInfo.CurrentCulture)
        };
        var json = JsonSerializer.Serialize(settings, _serializerOptions);

        await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);
        await ReadSettingsAsync().ConfigureAwait(false);

        return newId;
    }

    internal void ClearSettings() => _settings = null;


    public static async Task<ModSettings> ReadSettingsAsync(string fullPath)
    {
        if (!File.Exists(fullPath))
            throw new ModSettingsNotFoundException($"Settings file not found. Path: {fullPath}");
        if (Path.GetExtension(fullPath) != ".json")
            throw new InvalidOperationException($"Settings file is not a json file. Path: {fullPath}");

        var json = await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);

        var settings = InternalReadSettings(null, json);
        return settings;
    }

    public async Task<ModSettings> ReadSettingsAsync(bool useCache = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
            throw new ModSettingsNotFoundException($"Settings file not found. Path: {_settingsFilePath}");

        if (useCache && _settings is not null)
            return _settings;

        var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken).ConfigureAwait(false);

        var modSettings = InternalReadSettings(_skinMod, json);
        cancellationToken.ThrowIfCancellationRequested();
        _settings = modSettings;
        return modSettings;
    }

    private static ModSettings InternalReadSettings(ISkinMod? skinMod, string json)
    {
        var settings = JsonSerializer.Deserialize<JsonModSettings>(json, _serializerOptions);

        if (settings is null)
            throw new JsonSerializationException("Failed to deserialize settings file. Return value is null");

        var modSettings = ModSettings.FromJsonSkinSettings(skinMod, settings);

        return modSettings;
    }

    private Task SaveSettingsAsync(JsonModSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        return File.WriteAllTextAsync(_settingsFilePath, json);
    }


    public async Task SaveSettingsAsync(ModSettings modSettings, SaveSettingsOptions? options = null)
    {
        if (modSettings.ImagePath is not null && !SkinModHelpers.IsInModFolder(_skinMod, modSettings.ImagePath))
        {
            await CopyAndSetModImage(modSettings, modSettings.ImagePath, options?.DeleteOldImage ?? true)
                .ConfigureAwait(false);
        }

        if (modSettings.MergedIniPath is not null &&
            (!SkinModHelpers.IsInModFolder(_skinMod, modSettings.MergedIniPath) ||
             !File.Exists(modSettings.MergedIniPath.LocalPath)))
        {
            modSettings.MergedIniPath = null;
        }


        if (modSettings.ImagePath is null)
        {
            var oldSettings = _settings ?? await ReadSettingsAsync().ConfigureAwait(false);
            if ((options?.DeleteOldImage ?? true) && IsJasmImageFile(oldSettings.ImagePath))
                DeleteOldImage(oldSettings.ImagePath);
        }


        var jsonSkinSettings = modSettings.ToJsonSkinSettings(_skinMod);
        await SaveSettingsAsync(jsonSkinSettings).ConfigureAwait(false);
        _settings = modSettings;
    }

    public OneOf<ModSettings, SettingsNotLoaded> GetSettingsLegacy()
    {
        if (_settings is null)
            return new SettingsNotLoaded();

        return _settings;
    }

    private async Task CopyAndSetModImage(ModSettings modSettings, Uri imagePath, bool deleteOldImage = true)
    {
        // TODO: If you're saving a modified cached modSettings after the mod has been toggled enabled/disabled, imagePath.LocalPath won't exist
        var oldModSettings = _settings ?? await ReadSettingsAsync().ConfigureAwait(false);
        if (!File.Exists(imagePath.LocalPath))
            throw new FileNotFoundException("Image file not found.", imagePath.LocalPath);

        if (deleteOldImage && IsJasmImageFile(oldModSettings.ImagePath))
            DeleteOldImage(oldModSettings.ImagePath);


        var newImageFileName = ImageName + Path.GetExtension(imagePath.LocalPath);
        var newImagePath = Path.Combine(_skinMod.FullPath, newImageFileName);

        File.Copy(imagePath.LocalPath, newImagePath, true);
        modSettings.ImagePath = new Uri(newImagePath);
    }

    private bool IsJasmImageFile(Uri? imagePath)
    {
        if (imagePath is null)
            return false;

        if (!imagePath.IsFile)
            return false;

        var fsPath = imagePath.LocalPath;

        return Path.GetFileName(fsPath).StartsWith(ImageName, StringComparison.OrdinalIgnoreCase)
               && SkinModHelpers.IsInModFolder(_skinMod, imagePath);
    }

    private static void DeleteOldImage(Uri? oldImageUri)
    {
        if (oldImageUri is null || !File.Exists(oldImageUri.LocalPath))
            return;

        File.Delete(oldImageUri.LocalPath);
    }


    public async Task SetLastCheckedTimeAsync(DateTime dateTime)
    {
        var settings = _settings ?? await ReadSettingsAsync().ConfigureAwait(false);
        settings.LastChecked = dateTime;

        await SaveSettingsAsync(settings).ConfigureAwait(false);
    }

    public bool TryGetSettings([NotNullWhen(true)] out ModSettings? modSettings)
    {
        modSettings = _settings;
        return modSettings is not null;
    }

    /// <summary>
    /// This is helper wrapper function for <see cref="ReadSettingsAsync(bool, CancellationToken)"/> that returns null instead of throwing an exception.
    /// </summary>
    public async Task<ModSettings?> TryReadSettingsAsync(bool useCache = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReadSettingsAsync(useCache, cancellationToken).ConfigureAwait(false);
        }
        catch (ModSettingsNotFoundException)
        {
            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to read mod settings file. Path: {Path}", _settingsFilePath);
        }

        return null;
    }
}

public class SaveSettingsOptions
{
    public bool DeleteOldImage { get; set; } = true;
}

public struct SettingsNotLoaded
{
}