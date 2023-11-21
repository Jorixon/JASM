using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.Models;

public partial class ModModel : ObservableObject, IEquatable<ModModel>
{
    public Guid Id { get; private init; }
    public IModdableObject Character { get; private init; }
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _folderName = string.Empty;
    [ObservableProperty] private string _folderPath = string.Empty;


    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _modUrl = string.Empty;
    [ObservableProperty] private string _modVersion = string.Empty;
    [ObservableProperty] private DateTime _dateAdded;

    [ObservableProperty] private Uri _imagePath = PlaceholderImagePath;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _characterSkinOverride = string.Empty;

    [ObservableProperty] private ObservableCollection<ModNotification> _modNotifications = new();

    public static readonly Uri PlaceholderImagePath =
        new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\ModPanePlaceholder.webp"));

    public ObservableCollection<SkinModKeySwapModel> SkinModKeySwaps { get; set; } = new();


    private Action<ModModel>? _toggleMod = null;


    /// <summary>
    /// Needed for certain ui components.
    /// </summary>
    public ModModel()
    {
        Id = Guid.Empty;
        Character = null!;
    }


    public static ModModel FromMod(CharacterSkinEntry modEntry)
    {
        return FromMod(modEntry.Mod, modEntry.ModList.Character, modEntry.IsEnabled);
    }

    public static ModModel FromMod(ISkinMod skinMod, IModdableObject character, bool isEnabled)
    {
        var name = skinMod.Name;
        if (!string.IsNullOrWhiteSpace(name))
            name = ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(name);

        var modModel = new ModModel
        {
            Id = skinMod.Id,
            Character = character,
            Name = name,
            FolderName = skinMod.Name,
            FolderPath = skinMod.FullPath,
            IsEnabled = isEnabled
        };

        if (skinMod.Settings.GetSettingsLegacy().TryPickT0(out var settings, out _))
            modModel.WithModSettings(settings);

        if (skinMod.KeySwaps is not null && skinMod.KeySwaps.GetKeySwaps().TryPickT0(out var keySwaps, out _))
            modModel.SetKeySwaps(keySwaps);


        return modModel;
    }

    public ModModel WithModSettings(ModSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.CustomName))
            Name = settings.CustomName;

        ModUrl = settings.ModUrl?.ToString() ?? string.Empty;
        ModVersion = settings.Version ?? string.Empty;
        ImagePath = settings.ImagePath ?? PlaceholderImagePath;
        Author = settings.Author ?? string.Empty;
        CharacterSkinOverride = settings.CharacterSkinOverride ?? string.Empty;
        DateAdded = settings.DateAdded ?? DateTime.MinValue;
        return this;
    }


    public void SetKeySwaps(IEnumerable<KeySwapSection> keySwaps)
    {
        SkinModKeySwaps.Clear();
        foreach (var keySwapModel in keySwaps)
            SkinModKeySwaps.Add(SkinModKeySwapModel.FromKeySwapSettings(keySwapModel));
    }

    public ModModel WithToggleModDelegate(Action<ModModel> toggleMod)
    {
        _toggleMod = toggleMod;
        return this;
    }

    public override string ToString()
    {
        return "ModModel: " + Name + " (" + Id + ")";
    }


    // Due to some binding issues with datagrids, this is a hacky way to get the toggle button to work.
    [RelayCommand]
    private Task ToggleModAsync()
    {
        if (_toggleMod is not null)
            try
            {
                _toggleMod(this);
            }
            catch (Exception e)
            {
                App.GetService<NotificationManager>().ShowNotification($"An error occurred " +
                                                                       (IsEnabled ? "disabling" : "enabling") +
                                                                       $" the mod: {Name}",
                    e.ToString(), null);
            }

        return Task.CompletedTask;
    }

    public bool Equals(ModModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ModModel)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }


    public bool SettingsEquals(ModModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name && ModUrl == other.ModUrl && ModVersion == other.ModVersion &&
               ImagePath == other.ImagePath && Author == other.Author &&
               SkinModKeySwaps.SequenceEqual(other.SkinModKeySwaps);
    }
}