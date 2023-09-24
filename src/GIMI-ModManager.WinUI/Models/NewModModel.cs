using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.Models;

public partial class NewModModel : ObservableObject, IEquatable<NewModModel>
{
    public Guid Id { get; private init; }
    public GenshinCharacter Character { get; private init; }
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _folderName = string.Empty;


    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _modUrl = string.Empty;
    [ObservableProperty] private string _modVersion = string.Empty;

    [ObservableProperty] private Uri _imagePath = PlaceholderImagePath;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _characterSkinOverride = string.Empty;

    [ObservableProperty] private ObservableCollection<ModNotification> _modNotifications = new ();

    public static readonly Uri PlaceholderImagePath =
        new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\ModPanePlaceholder.webp"));

    public ObservableCollection<SkinModKeySwapModel> SkinModKeySwaps { get; set; } = new();


    private Action<NewModModel>? _toggleMod = null;


    /// <summary>
    /// Needed for certain ui components.
    /// </summary>
    public NewModModel()
    {
        Id = Guid.Empty;
        Character = null!;
    }


    public static NewModModel FromMod(CharacterSkinEntry modEntry)
    {
        var name = modEntry.Mod.Name;
        if (!string.IsNullOrWhiteSpace(name))
            name = modEntry.Mod.Name.Replace(
                name.StartsWith(CharacterModList.DISABLED_PREFIX) ? "DISABLED_" : "DISABLED", "");

        var modModel = new NewModModel
        {
            Id = modEntry.Id,
            Character = modEntry.ModList.Character,
            Name = string.IsNullOrWhiteSpace(name) ? modEntry.Mod.CustomName : name,
            FolderName = modEntry.Mod.Name,
            IsEnabled = modEntry.IsEnabled
        };

        if (modEntry.Mod.CachedSkinModSettings is { } settings)
            modModel.WithModSettings(settings);


        if (modEntry.Mod.CachedKeySwaps is { } keySwaps)
            modModel.SetKeySwaps(keySwaps);

        return modModel;
    }

    public NewModModel WithModSettings(SkinModSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.CustomName))
            Name = settings.CustomName;

        ModUrl = settings.ModUrl ?? string.Empty;
        ModVersion = settings.Version ?? string.Empty;
        ImagePath = string.IsNullOrWhiteSpace(settings.ImagePath)
            ? PlaceholderImagePath
            : new Uri(settings.ImagePath, UriKind.Absolute);
        Author = settings.Author ?? string.Empty;
        CharacterSkinOverride = settings.CharacterSkinOverride ?? string.Empty;
        return this;
    }

    public SkinModSettings ToModSettings()
    {
        return new SkinModSettings
        {
            CustomName = Name,
            Author = Author,
            Version = ModVersion,
            ModUrl = ModUrl,
            ImagePath = ImagePath.Equals(PlaceholderImagePath) ? string.Empty : ImagePath.ToString(),
            CharacterSkinOverride = string.IsNullOrWhiteSpace(CharacterSkinOverride)
                ? null
                : CharacterSkinOverride
        };
    }

    public void SetKeySwaps(IEnumerable<SkinModKeySwap> keySwaps)
    {
        SkinModKeySwaps.Clear();
        foreach (var keySwapModel in keySwaps)
            SkinModKeySwaps.Add(SkinModKeySwapModel.FromKeySwapSettings(keySwapModel));
    }

    public NewModModel WithToggleModDelegate(Action<NewModModel> toggleMod)
    {
        _toggleMod = toggleMod;
        return this;
    }

    public override string ToString()
    {
        return "NewModModel: " + Name + " (" + Id + ")";
    }


    // Due to some binding issues with datagrids, this is a hacky way to get the toggle button to work.
    [RelayCommand]
    private async Task ToggleModAsync()
    {
        if (_toggleMod is not null)
            try
            {
                await Task.Run(() => _toggleMod(this));
                IsEnabled = !IsEnabled;
            }
            catch (Exception e)
            {
                App.GetService<NotificationManager>().ShowNotification($"An error occured " +
                                                                       (IsEnabled ? "disabling" : "enabling") +
                                                                       $" the mod: {Name}",
                    e.ToString(), TimeSpan.MaxValue);
            }
    }

    public bool Equals(NewModModel? other)
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
        return Equals((NewModModel)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }


    public bool SettingsEquals(NewModModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name && ModUrl == other.ModUrl && ModVersion == other.ModVersion &&
               ImagePath == other.ImagePath && Author == other.Author &&
               SkinModKeySwaps.SequenceEqual(other.SkinModKeySwaps);
    }
}