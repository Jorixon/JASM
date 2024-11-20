using System.Collections.ObjectModel;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.Core.Services.CommandService.Models;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using Serilog;
using ScrollViewer = Microsoft.UI.Xaml.Controls.ScrollViewer;

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

    public static ModOverviewPageState PageState { get; } = new();
    public TaskCompletionSource<bool> ViewModelLoading { get; } = new();

    public ObservableCollection<CategoryNode> Categories { get; } = new();

    public ObservableCollection<ModOverviewCommandVM> CommandDefinitions { get; } = new();

    private string _searchText = string.Empty;
    public BaseNode? GoToNode { get; private set; }

    [ObservableProperty] private string _targetPath = string.Empty;

    public async void OnNavigatedTo(object parameter)
    {
        await Refresh();
        await LoadCommands().ConfigureAwait(false);

        var goToId = default(InternalName);

        if (parameter is InternalName internalName)
        {
            goToId = internalName;
        }


        if (goToId is not null && GetAllNodes().FirstOrDefault(n => n.Id == goToId) is { } node)
        {
            if (node is ModModelNode modModelNode)
            {
                var moddableObjectNode = modModelNode.ParentModdableObject;
                var categoryNode = moddableObjectNode.ParentCategory;

                categoryNode.IsExpanded = true;
                moddableObjectNode.IsExpanded = true;
                GoToNode = modModelNode;
            }
            else if (node is ModdableObjectNode moddableObjectNode)
            {
                var categoryNode = moddableObjectNode.ParentCategory;

                categoryNode.IsExpanded = true;
                moddableObjectNode.IsExpanded = true;
                GoToNode = moddableObjectNode;
            }
            else if (node is CategoryNode categoryNode)
            {
                categoryNode.IsExpanded = true;
            }


            ViewModelLoading.SetResult(false);
        }
        else
        {
            ViewModelLoading.SetResult(true);
        }
    }

    public async Task RestoreState(ScrollViewer? scrollViewer)
    {
        SearchTextChangedHandler(PageState.SearchText);

        foreach (var node in GetAllNodes())
        {
            if (!PageState.NodesState.TryGetValue(node.Id, out var stateNode))
                continue;


            node.IsExpanded = stateNode.IsExpanded;
            node.IsVisible = stateNode.IsVisible;
        }

        if (scrollViewer is not null)
        {
            await Task.Delay(200);
            scrollViewer.ChangeView(null, PageState.ScrollPosition, null);
        }
    }

    public void OnNavigatedFrom()
    {
        PageState.SearchText = _searchText;

        PageState.NodesState = GetAllNodes().ToDictionary(node => node.Id, node => node);
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

    [RelayCommand]
    private async Task CloseOpenAll(string? option)
    {
        var categories = Categories.ToArray();
        Categories.Clear();
        await Task.Delay(100);

        if (option == "Close")
        {
            foreach (var baseNode in GetAllNodes(categories).Where(n => n is { IsVisible: true, IsExpanded: true })
                         .Reverse())
            {
                baseNode.IsExpanded = false;
            }
        }
        else
        {
            foreach (var baseNode in GetAllNodes(categories).Where(n => n is { IsVisible: true, IsExpanded: false }))
            {
                baseNode.IsExpanded = true;
            }
        }

        Categories.AddRange(categories);
    }

    private async Task Refresh()
    {
        Categories.Clear();

        var categories = _gameService.GetCategories();

        foreach (var category in categories)
        {
            var modObjects = _gameService.GetModdableObjects(category);
            var categoryNode = new CategoryNode(category,
                _skinManagerService.GetCategoryFolderPath(category).FullName);

            foreach (var moddableObject in modObjects)
            {
                var modList = _skinManagerService.GetCharacterModList(moddableObject);
                var moddableObjectNode =
                    new ModdableObjectNode(moddableObject, modList.AbsModsFolderPath, categoryNode);

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
                    moddableObjectNode.Mods.Add(new ModModelNode(modModel, moddableObjectNode));


                if (moddableObjectNode.Mods.Count > 0)
                    categoryNode.ModdableObjects.Add(moddableObjectNode);
            }

            Categories.Add(categoryNode);
        }
    }


    public void SearchTextChangedHandler(string? text)
    {
        _searchText = (text ?? string.Empty).Trim();

        if (_searchText.IsNullOrEmpty())
        {
            foreach (var category in Categories)
            {
                category.IsVisible = true;
                category.IsExpanded = false;

                foreach (var moddableObject in category.ModdableObjects)
                {
                    moddableObject.IsVisible = true;
                    moddableObject.IsExpanded = false;

                    foreach (var mod in moddableObject.Mods)
                    {
                        mod.IsVisible = true;
                    }
                }
            }

            return;
        }

        foreach (var category in Categories)
        {
            foreach (var moddableObject in category.ModdableObjects)
            {
                foreach (var mod in moddableObject.Mods)
                {
                    var isMatch = mod.Mod.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

                    mod.IsVisible = isMatch;
                }

                var modChildMatch = moddableObject.Mods.Any(mod => mod.IsVisible);
                moddableObject.IsVisible = modChildMatch;
                moddableObject.IsExpanded = modChildMatch;
            }

            var modObjectMatch = category.ModdableObjects.Any(mod => mod.IsVisible);
            category.IsVisible = modObjectMatch;
            category.IsExpanded = modObjectMatch;
        }
    }


    private IEnumerable<BaseNode> GetAllNodes(IEnumerable<CategoryNode>? categoryNodes = null)
    {
        foreach (var categoryNode in categoryNodes ?? Categories)
        {
            yield return categoryNode;

            foreach (var moddableObjectNode in categoryNode.ModdableObjects)
            {
                yield return moddableObjectNode;

                foreach (var modModelNode in moddableObjectNode.Mods)
                {
                    yield return modModelNode;
                }
            }
        }
    }

    [RelayCommand]
    private void GoToCharacter(object? characterObj)
    {
        if (characterObj is not IModdableObject character)
        {
            return;
        }

        App.GetService<INavigationService>().NavigateToCharacterDetails(character.InternalName);
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

public partial class BaseNode : ObservableObject
{
    public string Id { get; }

    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _isExpanded;

    public BaseNode(string id)
    {
        Id = id;
    }
}

public interface IHasChildItems<out T> where T : BaseNode
{
    public IEnumerable<T> ChildItems { get; }
}

public partial class CategoryNode : BaseNode, IHasChildItems<ModdableObjectNode>
{
    public string DisplayName { get; init; }

    public ObservableCollection<ModdableObjectNode> ModdableObjects { get; } = new();
    public IEnumerable<ModdableObjectNode> ChildItems => ModdableObjects;

    public string FolderPath { get; }

    public CategoryNode(ICategory category, string folderPath) : base(category.InternalName)
    {
        DisplayName = category.DisplayNamePlural;
        FolderPath = folderPath;
    }
}

public partial class ModdableObjectNode : BaseNode, IHasChildItems<ModModelNode>
{
    [ObservableProperty] private IModdableObject _moddableObject;
    public Uri ImagePath { get; } = App.GetService<ImageHandlerService>().PlaceholderImageUri;

    public CategoryNode ParentCategory { get; }
    public ObservableCollection<ModModelNode> Mods { get; } = new();

    public IEnumerable<ModModelNode> ChildItems => Mods;

    public string FolderPath { get; }

    public ModdableObjectNode(IModdableObject moddableObject, string folderPath, CategoryNode parentCategory) : base(
        moddableObject.InternalName)
    {
        FolderPath = folderPath;
        ParentCategory = parentCategory;
        ModdableObject = moddableObject;

        if (moddableObject is IImageSupport { ImageUri: not null } imageSupport)
            ImagePath = imageSupport.ImageUri;
    }
}

public partial class ModModelNode : BaseNode
{
    public ModModel Mod { get; }

    public ModdableObjectNode ParentModdableObject { get; }

    public ModModelNode(ModModel mod, ModdableObjectNode parentModdableObject) : base(mod.Id.ToString())
    {
        Mod = mod;
        ParentModdableObject = parentModdableObject;
    }
}

public class ModOverviewPageState : EventArgs
{
    public string SearchText { get; set; } = string.Empty;
    public double ScrollPosition { get; set; }
    public Dictionary<string, BaseNode> NodesState { get; set; } = new();
}