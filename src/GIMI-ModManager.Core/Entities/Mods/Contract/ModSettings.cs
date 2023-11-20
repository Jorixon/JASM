using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.FileModels;
using GIMI_ModManager.Core.Entities.Mods.Helpers;

namespace GIMI_ModManager.Core.Entities.Mods.Contract;

public record ModSettings
{
    public ModSettings(Guid id, string? customName = null, string? author = null, string? version = null,
        Uri? modUrl = null, Uri? imagePath = null, string? characterSkinOverride = null, string? description = null,
        DateTime? dateAdded = null,
        DateTime? lastChecked = null)
    {
        Id = id;
        CustomName = customName;
        Author = author;
        Version = version;
        ModUrl = modUrl;
        ImagePath = imagePath;
        CharacterSkinOverride = characterSkinOverride;
        Description = description;
        DateAdded = dateAdded;
        LastChecked = lastChecked;
    }

    public ModSettings DeepCopyWithProperties(string? customName = null, string? newCharacterSkinOverride = null,
        DateTime? newLastChecked = null)
    {
        return new ModSettings(
            Id,
            customName ?? CustomName,
            Author,
            Version,
            ModUrl,
            ImagePath,
            newCharacterSkinOverride ?? CharacterSkinOverride,
            Description,
            DateAdded,
            newLastChecked ?? LastChecked
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
    public string? Description { get; internal set; }

    public DateTime? DateAdded { get; internal set; }

    public DateTime? LastChecked { get; internal set; }


    internal static ModSettings FromJsonSkinSettings(ISkinMod skinMod, JsonModSettings settings)
    {
        return new ModSettings
        {
            Id = SkinModHelpers.StringToGuid(settings.Id),
            CustomName = settings.CustomName,
            Author = settings.Author,
            Version = settings.Version,
            ModUrl = SkinModHelpers.StringUrlToUri(settings.ModUrl),
            ImagePath = SkinModHelpers.RelativeModPathToAbsPath(skinMod.FullPath, settings.ImagePath),
            CharacterSkinOverride = settings.CharacterSkinOverride,
            Description = settings.Description,
            DateAdded = DateTime.TryParse(settings.DateAdded, out var dateAdded) ? dateAdded : null,
            LastChecked = DateTime.TryParse(settings.LastChecked, out var lastChecked) ? lastChecked : null
        };
    }

    internal JsonModSettings ToJsonSkinSettings(ISkinMod skinMod)
    {
        return new JsonModSettings
        {
            Id = Id.ToString(),
            CustomName = CustomName,
            Author = Author,
            Version = Version,
            ModUrl = ModUrl?.ToString(),
            ImagePath = SkinModHelpers.UriPathToModRelativePath(skinMod, ImagePath?.LocalPath),
            CharacterSkinOverride = CharacterSkinOverride,
            Description = Description,
            DateAdded = DateAdded?.ToString(),
            LastChecked = LastChecked?.ToString()
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