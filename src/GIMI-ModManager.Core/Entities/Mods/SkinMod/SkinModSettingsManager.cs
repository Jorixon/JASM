using System.Text.Json;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.FileModels;
using GIMI_ModManager.Core.Entities.Mods.Helpers;
using Newtonsoft.Json;
using OneOf;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GIMI_ModManager.Core.Entities.Mods.SkinMod;

public class SkinModSettingsManager
{
    private readonly ISkinMod _skinMod;
    private const string configFileName = ".JASM_ModConfig.json";
    private const string ImageName = ".JASM_Cover";

    private string _settingsFilePath => Path.Combine(_skinMod.FullPath, configFileName);

    private ModSettings? _settings;

    private readonly JsonSerializerOptions _serializerOptions = new()
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

            if (updateSettings)
                await SaveSettingsAsync(modSettings).ConfigureAwait(false);

            return modSettings.Id;
        }

        var newId = Guid.NewGuid();
        var settings = new JsonModSettings() { Id = newId.ToString() };
        var json = JsonSerializer.Serialize(settings, _serializerOptions);

        await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);
        await ReadSettingsAsync().ConfigureAwait(false);

        return newId;
    }

    internal void ClearSettings() => _settings = null;


    public async Task<ModSettings> ReadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
            throw new ModSettingsNotFoundException($"Settings file not found. Path: {_settingsFilePath}");

        var json = await File.ReadAllTextAsync(_settingsFilePath);


        var settings = JsonSerializer.Deserialize<JsonModSettings>(json, _serializerOptions);


        if (settings is null)
            throw new JsonSerializationException("Failed to deserialize settings file. Return value is null");

        var modSettings = ModSettings.FromJsonSkinSettings(_skinMod, settings);
        _settings = modSettings;
        return modSettings;
    }

    private Task SaveSettingsAsync(JsonModSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        return File.WriteAllTextAsync(_settingsFilePath, json);
    }


    public async Task SaveSettingsAsync(ModSettings modSettings)
    {
        if (modSettings.ImagePath is not null && !ModsHelpers.IsInModFolder(_skinMod, modSettings.ImagePath))
            await CopyAndSetModImage(modSettings, modSettings.ImagePath);


        if (modSettings.ImagePath is null)
            await ClearModImage();


        var jsonSkinSettings = modSettings.ToJsonSkinSettings(_skinMod);
        await SaveSettingsAsync(jsonSkinSettings);
        _settings = modSettings;
    }

    public OneOf<ModSettings, SettingsNotLoaded> GetSettings()
    {
        if (_settings is null)
            return new SettingsNotLoaded();

        return _settings;
    }

    private async Task CopyAndSetModImage(ModSettings modSettings, Uri imagePath)
    {
        var oldModSettings = _settings ?? await ReadSettingsAsync();
        if (!File.Exists(imagePath.LocalPath))
            throw new FileNotFoundException("Image file not found.", imagePath.LocalPath);


        DeleteOldImage(oldModSettings.ImagePath);


        var newImageFileName = ImageName + Path.GetExtension(imagePath.LocalPath);
        var newImagePath = Path.Combine(_skinMod.FullPath, newImageFileName);

        File.Copy(imagePath.LocalPath, newImagePath, true);
        modSettings.ImagePath = new Uri(newImagePath);
    }


    public async Task ClearModImage()
    {
        var modSettings = _settings ?? await ReadSettingsAsync();

        DeleteOldImage(modSettings.ImagePath);
    }

    private static void DeleteOldImage(Uri? oldImageUri)
    {
        if (oldImageUri is null || !File.Exists(oldImageUri.LocalPath))
            return;

        File.Delete(oldImageUri.LocalPath);
    }
}

public class ModSettingsNotFoundException : Exception
{
    public ModSettingsNotFoundException(string message) : base(message)
    {
    }
}

public struct SettingsNotLoaded
{
}