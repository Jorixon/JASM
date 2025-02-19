using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Xaml;
using GIMI_ModManager.WinUI.Services;
using WinRT.Interop;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels
{
    public class ModGridItemVm : ObservableObject
    {
        private readonly ModModel _modModel;
        private Uri? _originalImagePath;

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

        public bool CanSaveImage => ImagePath != _originalImagePath;

        public ModGridItemVm(
            ModModel modModel,
            IAsyncRelayCommand toggleModCommand,
            IAsyncRelayCommand openModFolderCommand,
            IAsyncRelayCommand openModUrlCommand,
            IAsyncRelayCommand deleteModCommand
        )
        {
            _modModel = modModel;
            _originalImagePath = modModel.ImagePath;
            ToggleModCommand = toggleModCommand;
            OpenModFolderCommand = openModFolderCommand;
            OpenModUrlCommand = openModUrlCommand;
            DeleteModCommand = deleteModCommand;
        }


        public IAsyncRelayCommand ToggleModCommand { get; }

        public IAsyncRelayCommand OpenModFolderCommand { get; }

        public IAsyncRelayCommand OpenModUrlCommand { get; }

        public IAsyncRelayCommand DeleteModCommand { get; }

        public IAsyncRelayCommand PasteImageFromClipboardCommand => new AsyncRelayCommand(async () =>
        {
            var imageHandlerService = App.GetService<ImageHandlerService>();

            var clipboardHasValidImageResult = await imageHandlerService.ClipboardContainsImageAsync();
            if (!clipboardHasValidImageResult.Result)
            {
                return;
            }

            var imagePath = await imageHandlerService.GetImageFromClipboardAsync(clipboardHasValidImageResult.DataPackage);
            if (imagePath == null)
            {
                return;
            }

            _modModel.ImagePath = imagePath;
            OnPropertyChanged(nameof(ImagePath));
            OnPropertyChanged(nameof(CanSaveImage));
        });

        public IRelayCommand ClearImageCommand => new RelayCommand(() =>
        {
            _modModel.ImagePath = ImageHandlerService.StaticPlaceholderImageUri;
            OnPropertyChanged(nameof(ImagePath));
            OnPropertyChanged(nameof(CanSaveImage));
        });

        public IAsyncRelayCommand SaveImageCommand => new AsyncRelayCommand(async () =>
        {
            var modSettingsService = App.GetService<ModSettingsService>();
            var notificationService = App.GetService<NotificationManager>();

            try
            {
                var updateRequest = new UpdateSettingsRequest
                {
                    SetImagePath = ImagePath
                };

                var result = await modSettingsService.SaveSettingsAsync(Id, updateRequest);

                if (result?.Notification is not null)
                    notificationService.ShowNotification(result.Notification);

                _originalImagePath = ImagePath;
                OnPropertyChanged(nameof(CanSaveImage));
            }
            catch (Exception e)
            {
                notificationService.ShowNotification("Failed to save image", e.Message, null);
            }
        },
        () => CanSaveImage);
    }
}