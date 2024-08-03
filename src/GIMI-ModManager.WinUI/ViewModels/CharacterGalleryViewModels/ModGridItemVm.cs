using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels
{
    public class ModGridItemVm : ObservableObject
    {
        private readonly ModModel _modModel;

        public Guid Id => _modModel.Id;
        public IModdableObject Character => _modModel.Character;
        public string Name => _modModel.Name;
        public string Author => _modModel.Author;
        public DateTime DateAdded => _modModel.DateAdded;
        public string DateAddedView => $"Added to JASM on: {DateAdded}";
        public TimeSpan TimeSinceAdded => DateTime.Now - DateAdded;
        public string TimeSinceFormated => FormaterHelpers.FormatTimeSinceAdded(TimeSinceAdded);
        public Uri ImagePath => _modModel.ImagePath;
        public Uri? ModUrl => string.IsNullOrWhiteSpace(_modModel.ModUrl) ? null : new Uri(_modModel.ModUrl);
        public bool HasModUrl => ModUrl is not null;
        public string NameTooltip => $"Custom Name: {Name}\nFolder Name: {FolderName}";
        public string ButtonText => _modModel.IsEnabled ? "Disable" : "Enable";

        public string FolderName
        {
            get => _modModel.FolderName;
            set
            {
                _modModel.FolderName = value;
                OnPropertyChanged(nameof(NameTooltip));
                OnPropertyChanged();
            }
        }

        public string FolderPath
        {
            get => _modModel.FolderPath;
            set
            {
                _modModel.FolderPath = value;
                OnPropertyChanged(nameof(NameTooltip));
                OnPropertyChanged();
            }
        }

        public Style? ButtonStyle => _modModel.IsEnabled
            ? (Style)Application.Current.Resources["AccentButtonStyle"]
            : (Style)Application.Current.Resources["DefaultButtonStyle"];

        public bool IsEnabled
        {
            get => _modModel.IsEnabled;
            set
            {
                _modModel.IsEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ButtonText));
                OnPropertyChanged(nameof(ButtonStyle));
            }
        }

        public ModGridItemVm(
            ModModel modModel,
            IAsyncRelayCommand toggleModCommand,
            IAsyncRelayCommand openModFolderCommand,
            IAsyncRelayCommand openModUrlCommand,
            IAsyncRelayCommand deleteModCommand
        )
        {
            _modModel = modModel;
            ToggleModCommand = toggleModCommand;
            OpenModFolderCommand = openModFolderCommand;
            OpenModUrlCommand = openModUrlCommand;
            DeleteModCommand = deleteModCommand;
        }


        public IAsyncRelayCommand ToggleModCommand { get; }

        public IAsyncRelayCommand OpenModFolderCommand { get; }

        public IAsyncRelayCommand OpenModUrlCommand { get; }

        public IAsyncRelayCommand DeleteModCommand { get; }
    }
}