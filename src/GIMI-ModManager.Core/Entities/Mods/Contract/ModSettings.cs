using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.FileModels;
using GIMI_ModManager.Core.Entities.Mods.Helpers;

namespace GIMI_ModManager.Core.Entities.Mods.Contract;

public record ModSettings
{
    public ModSettings(Guid id, string? customName = null, string? author = null, string? version = null,
        Uri? modUrl = null, Uri? imagePath = null, string? characterSkinOverride = null)
    {
        Id = id;
        CustomName = customName;
        Author = author;
        Version = version;
        ModUrl = modUrl;
        ImagePath = imagePath;
        CharacterSkinOverride = characterSkinOverride;
    }

    public ModSettings DeepCopyWithProperties(string? characterSkinOverride = null)
    {
        return new ModSettings(
            Id,
            CustomName,
            Author,
            Version,
            ModUrl,
            ImagePath,
            characterSkinOverride ?? CharacterSkinOverride
        );
    }

    internal ModSettings()
    {
    }

    public Guid Id { get; internal set; }

    public string? CustomName { get; internal set; }

    public string? Author { get; internal set; }

    public string? Version { get; internal set; }

    public Uri? ModUrl { get; internal set; }

    public Uri? ImagePath { get; internal set; }

    public string? CharacterSkinOverride { get; internal set; }


    internal static ModSettings FromJsonSkinSettings(ISkinMod skinMod, JsonModSettings settings)
    {
        return new ModSettings
        {
            Id = settings.Id,
            CustomName = settings.CustomName,
            Author = settings.Author,
            Version = settings.Version,
            ModUrl = ModsHelpers.StringUrlToUri(settings.ModUrl),
            ImagePath = ModsHelpers.RelativeModPathToAbsPath(skinMod.FullPath, settings.ImagePath),
            CharacterSkinOverride = settings.CharacterSkinOverride
        };
    }

    internal JsonModSettings ToJsonSkinSettings(ISkinMod skinMod)
    {
        return new JsonModSettings
        {
            Id = Id,
            CustomName = CustomName,
            Author = Author,
            Version = Version,
            ModUrl = ModUrl?.ToString(),
            ImagePath = ModsHelpers.UriPathToModRelativePath(skinMod, ImagePath?.ToString()),
            CharacterSkinOverride = CharacterSkinOverride
        };
    }


    public bool SettingsEquals(ModSettings? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (ReferenceEquals(other, null)) return false;

        if (Id != other.Id) return false;
        if (CustomName != other.CustomName) return false;
        if (Author != other.Author) return false;
        if (Version != other.Version) return false;
        if (ModUrl != other.ModUrl) return false;
        if (ImagePath != other.ImagePath) return false;
        if (CharacterSkinOverride != other.CharacterSkinOverride) return false;

        return true;
    }
}