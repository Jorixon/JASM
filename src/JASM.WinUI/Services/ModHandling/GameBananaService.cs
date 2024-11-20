using System.Text.Json.Serialization;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services.GameBanana;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class GameBananaService(
    ILogger logger,
    ISkinManagerService skinManagerService,
    GameBananaCoreService gameBananaCoreService)
{
    private readonly ILogger _logger = logger.ForContext<GameBananaService>();
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly GameBananaCoreService _gameBananaCoreService = gameBananaCoreService;


    public async Task<ModPageInfo> GetModInfoAsync(Guid modId, CancellationToken cancellationToken = default)
    {
        var mod = _skinManagerService.GetModById(modId);
        if (mod is null)
            throw new InvalidOperationException($"Mod with id {modId} not found");

        var modSettings = await mod.Settings.ReadSettingsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (modSettings.ModUrl is null)
            throw new InvalidOperationException("Mod url is null");


        return await GetModInfoAsync(modSettings.ModUrl, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ModPageInfo> GetModInfoAsync(Uri modUrl, CancellationToken cancellationToken = default)
    {
        var modGbId = GetModIdFromUri(modUrl);

        if (modGbId is null)
            throw new InvalidGameBananaUrlException($"Invalid GameBanana url: {modUrl}");

        var result = await _gameBananaCoreService.GetModProfileAsync(new GbModId(modGbId), cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
            throw new InvalidOperationException($"Mod with id {modGbId} not found");

        return result;
    }

    public async Task<ModsRetrievedResult> GetAvailableModFiles(Guid modId, bool ignoreCache = false,
        CancellationToken cancellationToken = default)
    {
        var mod = _skinManagerService.GetModById(modId);
        if (mod is null)
            throw new InvalidOperationException($"Mod with id {modId} not found");


        var modSettings = await mod.Settings.ReadSettingsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (modSettings.ModUrl is null)
            throw new InvalidOperationException("Mod url is null");

        var modGbId = GetModIdFromUri(modSettings.ModUrl);

        if (modGbId is null)
            throw new InvalidGameBananaUrlException(
                $"Invalid GameBanana url: {modSettings.ModUrl} | For mod {mod.FullPath}");

        var result = await _gameBananaCoreService
            .GetModFilesInfoAsync(new GbModId(modGbId), ignoreCache: ignoreCache, ct: cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
            throw new InvalidOperationException($"Mod with id {modGbId} not found");


        return new ModsRetrievedResult()
        {
            ModId = modGbId,
            LastCheck = modSettings.LastChecked ?? modSettings.DateAdded ?? DateTime.MinValue,
            CheckTime = DateTime.Now,
            ModFiles = result,
            SitePageUrl = modSettings.ModUrl
        };
    }

    private string? GetModIdFromUri(Uri modUrl)
    {
        var segments = modUrl.Segments;
        if (segments.Length < 2) return null;


        if (!modUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
            !modUrl.Host.Equals("gamebanana.com", StringComparison.OrdinalIgnoreCase))
            return null;


        var modId = segments.Last();
        return modId;
    }
}

public record ModsRetrievedResult
{
    public required string ModId { get; init; }

    public required DateTime CheckTime { get; init; }
    public required DateTime LastCheck { get; init; }
    public required Uri SitePageUrl { get; init; } = null!;
    [JsonIgnore] public bool AnyNewMods => ModFiles.Any(m => m.DateAdded > LastCheck);
    public required IReadOnlyList<ModFileInfo> ModFiles { get; init; }
}