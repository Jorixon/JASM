using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.WinUI.Models;
using OneOf;
using OneOf.Types;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class KeySwapService
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;

    public KeySwapService(ISkinManagerService skinManagerService, ILogger logger,
        NotificationManager notificationManager)
    {
        _skinManagerService = skinManagerService;
        _notificationManager = notificationManager;
        _logger = logger.ForContext<KeySwapService>();
    }

    public async Task<OneOf<Success, NotFound, ModNotFound, Error<Exception>>> SaveKeySwapsAsync(ModModel modModel)
    {
        var skinMod = _skinManagerService.GetModById(modModel.Id);

        if (skinMod is null)
            return new ModNotFound(modModel.Id);


        if (skinMod.KeySwaps is null)
            return new NotFound();

        var keySwapSections = new List<KeySwapSection>(modModel.SkinModKeySwaps.Count);

        foreach (var modModelSkinModKeySwap in modModel.SkinModKeySwaps)
        {
            var variants = int.TryParse(modModelSkinModKeySwap.VariationsCount, out var variantsCount)
                ? variantsCount
                : -1;

            var keySwapSection = new KeySwapSection()
            {
                SectionName = modModelSkinModKeySwap.SectionKey,
                ForwardKey = modModelSkinModKeySwap.ForwardHotkey,
                BackwardKey = modModelSkinModKeySwap.BackwardHotkey,
                Variants = variants == -1 ? null : variants,
                Type = modModelSkinModKeySwap.Type ?? "Unknown"
            };

            keySwapSections.Add(keySwapSection);
        }


        try
        {
            await Task.Run(() => skinMod.KeySwaps.SaveKeySwapConfiguration(keySwapSections));
            return new Success();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to save key swap configuration for mod {ModName}", skinMod.Name);
            _notificationManager.ShowNotification($"Failed to save key swap configuration for mod {skinMod.Name}",
                $"An error occurred when saving. Reason: {e.Message}", null);
            return new Error<Exception>(e);
        }
    }

    public async Task<OneOf<KeySwapSection[], NotFound, ModNotFound, Error<Exception>>> GetKeySwapsAsync(Guid modId)
    {
        var skinMod = _skinManagerService.GetModById(modId);

        if (skinMod is null)
            return new ModNotFound(modId);

        if (skinMod.KeySwaps is null)
        {
            _logger.Debug("Key swap manager for mod {ModName} is null", skinMod.Name);
            return new NotFound();
        }


        var getResult = skinMod.KeySwaps.GetKeySwaps();

        if (getResult.IsT0)
            return getResult.AsT0;

        try
        {
            await Task.Run(() => skinMod.KeySwaps.ReadKeySwapConfiguration());
            getResult = skinMod.KeySwaps.GetKeySwaps();

            return getResult.AsT0;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to read key swap configuration for mod {ModName}", skinMod.Name);
            return new Error<Exception>(e);
        }
    }
}