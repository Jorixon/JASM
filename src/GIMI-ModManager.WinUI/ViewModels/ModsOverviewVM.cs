using System.Collections.ObjectModel;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModsOverviewVM(
    ILogger logger,
    ISkinManagerService skinManagerService,
    IGameService gameService,
    CommandService commandService,
    CommandHandlerService commandHandlerService,
    NotificationManager notificationManager)
    : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger = logger.ForContext<ModsOverviewVM>();
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly IGameService _gameService = gameService;
    private readonly CommandService _commandService = commandService;
    private readonly CommandHandlerService _commandHandlerService = commandHandlerService;
    private readonly NotificationManager _notificationManager = notificationManager;


    public ObservableCollection<CategoryNode> Categories { get; } = new();

    public ObservableCollection<ModOverviewCommandVM> CommandDefinitions { get; } = new();

    [ObservableProperty] private string _targetPath = string.Empty;

    public async void OnNavigatedTo(object parameter)
    {
        await Refresh();
        await LoadCommands().ConfigureAwait(false);
    }

    public void OnNavigatedFrom()
    {
    }

    private async Task LoadCommands()
    {
        var commandDefinitions =
            await _commandHandlerService.GetCommandsThatContainSpecialVariablesAsync(SpecialVariables.TargetPath);
        CommandDefinitions.Clear();

        foreach (var commandDefinition in commandDefinitions)
        {
            CommandDefinitions.Add(new ModOverviewCommandVM(commandDefinition)
            {
                RunCommand = RunCommandCommand,
                OpenFolder = OpenFolderCommand
            });
        }
    }

    [RelayCommand]
    private async Task RunCommandAsync(ModOverviewCommandVM? commandDefinitionVM)
    {
        if (commandDefinitionVM is null || TargetPath.IsNullOrEmpty())
            return;


        var result = await Task.Run(async () => await _commandHandlerService.RunCommandAsync(commandDefinitionVM.Id,
            SpecialVariablesInput.CreateWithTargetPath(TargetPath)).ConfigureAwait(false));

        if (result.HasNotification)
            _notificationManager.ShowNotification(result.Notification);
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (TargetPath.IsNullOrEmpty() || !Directory.Exists(TargetPath))
            return;
        await Launcher.LaunchFolderPathAsync(TargetPath);
    }

    private async Task Refresh()
    {
        Categories.Clear();

        var categories = _gameService.GetCategories();

        foreach (var category in categories)
        {
            var modObjects = _gameService.GetModdableObjects(category);
            var categoryNode = new CategoryNode(category.DisplayNamePlural,
                _skinManagerService.GetCategoryFolderPath(category).FullName);

            foreach (var moddableObject in modObjects)
            {
                var modList = _skinManagerService.GetCharacterModList(moddableObject);
                var moddableObjectNode = new ModdableObjectNode(moddableObject, modList.AbsModsFolderPath);

                var mods = new List<ModModel>();
                foreach (var skinEntry in modList.Mods)
                {
                    var modModel = ModModel.FromMod(skinEntry);

                    var mod = skinEntry.Mod;

                    var skinSettings = mod.Settings.TryGetSettings(out var settings)
                        ? settings
                        : await mod.Settings.TryReadSettingsAsync();

                    if (skinSettings is not null)
                        modModel.WithModSettings(skinSettings);

                    mods.Add(modModel);
                }

                foreach (var modModel in mods.OrderByDescending(mod => mod.DateAdded))
                    moddableObjectNode.Mods.Add(modModel);


                if (moddableObjectNode.Mods.Count > 0)
                    categoryNode.ModdableObjects.Add(moddableObjectNode);
            }

            Categories.Add(categoryNode);
        }
    }
}

public partial class ModOverviewCommandVM : ObservableObject
{
    public ModOverviewCommandVM(CommandDefinition commandDefinition)
    {
        Id = commandDefinition.Id;
        DisplayName = commandDefinition.CommandDisplayName;

        var (fullCommand, workingDirectory) = commandDefinition.GetFullCommand(null);
        FullCommand = fullCommand;
        WorkingDirectory = workingDirectory ?? App.ROOT_DIR;
    }

    public Guid Id { get; set; }
    public string DisplayName { get; set; }

    public string FullCommand { get; set; }

    public string WorkingDirectory { get; set; }

    public required IAsyncRelayCommand RunCommand { get; init; }

    public required IAsyncRelayCommand OpenFolder { get; init; }

    [ObservableProperty] private string _targetPath = string.Empty;
}

public class CategoryNode : ObservableObject
{
    public string DisplayName { get; init; }

    public ObservableCollection<ModdableObjectNode> ModdableObjects { get; } = new();
    public string FolderPath { get; }

    public CategoryNode(string displayName, string folderPath)
    {
        DisplayName = displayName;
        FolderPath = folderPath;
    }
}

public partial class ModdableObjectNode : ObservableObject
{
    [ObservableProperty] private IModdableObject _moddableObject;
    public Uri ImagePath { get; } = App.GetService<ImageHandlerService>().PlaceholderImageUri;
    public ObservableCollection<ModModel> Mods { get; } = new();

    public string FolderPath { get; }

    public ModdableObjectNode(IModdableObject moddableObject, string folderPath)
    {
        FolderPath = folderPath;
        ModdableObject = moddableObject;

        if (moddableObject is IImageSupport { ImageUri: not null } imageSupport)
            ImagePath = imageSupport.ImageUri;
    }

    [RelayCommand]
    private void GoToCharacter(object? characterObj)
    {
        if (characterObj is not IModdableObject character)
        {
            return;
        }

        App.GetService<INavigationService>().NavigateTo(typeof(CharacterDetailsViewModel).FullName!, character);
    }
}

