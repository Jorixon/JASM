using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public class ModsOverviewVM : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGameService _gameService;

    public ObservableCollection<CategoryNode> Categories { get; } = new();

    public ModsOverviewVM(ILogger logger, ISkinManagerService skinManagerService, IGameService gameService)
    {
        _skinManagerService = skinManagerService;
        _gameService = gameService;
        _logger = logger.ForContext<ModsOverviewVM>();
    }

    public async void OnNavigatedTo(object parameter)
    {
        await Refresh().ConfigureAwait(false);
    }

    public void OnNavigatedFrom()
    {
    }


    private async Task Refresh()
    {
        Categories.Clear();

        var categories = _gameService.GetCategories();

        foreach (var category in categories)
        {
            var modObjects = _gameService.GetModdableObjects(category);
            var categoryNode = new CategoryNode(category.DisplayNamePlural);

            foreach (var moddableObject in modObjects)
            {
                var modList = _skinManagerService.GetCharacterModList(moddableObject);
                var moddableObjectNode = new ModdableObjectNode(moddableObject);

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

public class CategoryNode : ObservableObject
{
    public string DisplayName { get; init; }

    public ObservableCollection<ModdableObjectNode> ModdableObjects { get; } = new();

    public CategoryNode(string displayName, IEnumerable<ModdableObjectNode>? moddableObjects = null)
    {
        DisplayName = displayName;
        moddableObjects?.ForEach(ModdableObjects.Add);
    }
}

public partial class ModdableObjectNode : ObservableObject
{
    [ObservableProperty] private IModdableObject _moddableObject;
    public Uri ImagePath { get; } = App.GetService<ImageHandlerService>().PlaceholderImageUri;
    public ObservableCollection<ModModel> Mods { get; } = new();

    public ModdableObjectNode(IModdableObject moddableObject, IEnumerable<ModModel>? mods = null)
    {
        ModdableObject = moddableObject;

        if (moddableObject is IImageSupport { ImageUri: not null } imageSupport)
            ImagePath = imageSupport.ImageUri;

        mods?.ForEach(Mods.Add);
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