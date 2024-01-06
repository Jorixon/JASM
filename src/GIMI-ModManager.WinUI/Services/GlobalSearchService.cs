using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;

namespace GIMI_ModManager.WinUI.Services;

public sealed class GlobalSearchService
{
    private readonly IGameService _gameService;
    private readonly ISkinManagerService _skinManagerService;

    public GlobalSearchService(IGameService gameService, ISkinManagerService skinManagerService)
    {
        _gameService = gameService;
        _skinManagerService = skinManagerService;
    }
}