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

            if (modSettings.ImagePath is not null && !File.Exists(modSettings.ImagePath.LocalPath))
            {
                modSettings.ImagePath = null;
                updateSettings = true;
            }

            if (modSettings.ImagePath is null)
            {
                var images = DetectImages();
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

        var image = DetectImages().FirstOrDefault();

        var settings = new JsonModSettings()
        {
            Id = newId.ToString(), ImagePath = image?.LocalPath,
            DateAdded = DateTime.Now.ToString(CultureInfo.CurrentCulture)
        };
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

    private readonly string[] _imageNamePriority = new[] { ".jasm_cover", "preview", "cover" };

    public Uri[] DetectImages()
    {
        var modDir = new DirectoryInfo(_skinMod.FullPath);
        if (!modDir.Exists)
            return Array.Empty<Uri>();

        var images = new List<FileInfo>();
        foreach (var file in modDir.EnumerateFiles())
        {
            if (!_imageNamePriority.Any(i => file.Name.ToLower().StartsWith(i)))
                continue;


            var extension = file.Extension.ToLower();
            if (!_supportedImageExtensions.Contains(extension))
                continue;

            images.Add(file);
        }

        // Sort images by priority
        foreach (var imageName in _imageNamePriority.Reverse())
        {
            var image = images.FirstOrDefault(x => x.Name.ToLower().StartsWith(imageName));
            if (image is null)
                continue;

            images.Remove(image);
            images.Insert(0, image);
        }

        return images.Select(x => new Uri(x.FullName)).ToArray();
    }

    public async Task SetLastCheckedTimeAsync(DateTime dateTime)
    {
        var settings = _settings ?? await ReadSettingsAsync().ConfigureAwait(false);
        settings.LastChecked = dateTime;

        await SaveSettingsAsync(settings).ConfigureAwait(false);
    }

    public async Task<ModSettings?> TryReadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
            return null;

        try
        {
            return await ReadSettingsAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to read mod settings file. Path: {Path}", _settingsFilePath);
        }

        return null;
    }
}

public struct SettingsNotLoaded
{
}