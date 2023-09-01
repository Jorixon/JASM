using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.Models;

public partial class NewModModel : ObservableObject
{
    public Guid Id { get; private init; }
    public GenshinCharacter Character { get; private init; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _modUrl = string.Empty;
    [ObservableProperty] private string _modVersion = string.Empty;
    [ObservableProperty] private string _folderName = string.Empty;
    [ObservableProperty] private string _imageUri = string.Empty;

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
        {
            name = modEntry.Mod.Name.Replace("DISABLED_", "");
            name = modEntry.Mod.Name.Replace("DISABLED", "");
        }

        return new NewModModel
        {
            Id = modEntry.Id,
            Character = modEntry.ModList.Character,
            Name = string.IsNullOrWhiteSpace(name) ? modEntry.Mod.CustomName : name,
            FolderName = modEntry.Mod.Name,
            IsEnabled = modEntry.IsEnabled
        };
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
        {
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
    }
}