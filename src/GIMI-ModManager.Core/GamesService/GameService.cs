using System.Text.Json;
using GIMI_ModManager.Core.GamesService.JsonModels;
using Serilog;

namespace GIMI_ModManager.Core.GamesService;

public class GameService : IGameService
{
    private readonly ILogger _logger;

    private DirectoryInfo _assetsDirectory = null!;
    private DirectoryInfo _localSettingsDirectory = null!;
    private DirectoryInfo _languageOverrideDirectory = null!;
    public string GameName { get; private set; } = null!;
    public string GameShortName { get; private set; } = null!;

    public Elements Elements { get; private set; } = null!;

    public Classes Classes { get; private set; } = null!;

    public Regions Regions { get; private set; } = null!;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public GameService(ILogger logger)
    {
        _logger = logger;
    }


    public async Task InitializeAsync(string assetsDirectory, string localSettingsDirectory)
    {
        _assetsDirectory = new DirectoryInfo(assetsDirectory);
        if (!_assetsDirectory.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {_assetsDirectory.FullName}");

        _localSettingsDirectory = new DirectoryInfo(localSettingsDirectory);
        if (!_localSettingsDirectory.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {_localSettingsDirectory.FullName}");

        await InitializeRegionsAsync();

        InitializeElements();

        InitializeClasses();

        InitializeCharacters();
    }

    private async Task InitializeRegionsAsync()
    {
        var regionFileName = "regions.json";
        var regionsFilePath = Path.Combine(_assetsDirectory.FullName, regionFileName);

        if (!File.Exists(regionsFilePath))
            throw new FileNotFoundException($"{regionFileName} File not found at path: {regionsFilePath}");

        var regions =
            JsonSerializer.Deserialize<IEnumerable<JsonRegions>>(await File.ReadAllTextAsync(regionsFilePath),
                _jsonSerializerOptions) ?? throw new InvalidOperationException("Regions file is empty");

        Regions = Regions.InitializeRegions(regions);

        if (!LanguageOverrideAvailable())
            return;

        throw new NotImplementedException();
    }

    private void InitializeCharacters()
    {
        throw new NotImplementedException();
    }

    private void InitializeClasses()
    {
        throw new NotImplementedException();
    }

    private void InitializeElements()
    {
        throw new NotImplementedException();
    }

    private bool LanguageOverrideAvailable()
    {
        return false;
    }


    public List<IGameElement> GetElements()
    {
        throw new NotImplementedException();
    }

    public List<IGameClass> GetClasses()
    {
        throw new NotImplementedException();
    }

    public List<IRegion> GetRegions()
    {
        throw new NotImplementedException();
    }

    public List<ICharacter> GetCharacters()
    {
        throw new NotImplementedException();
    }
}

public class Regions
{
    private readonly List<Region> _regions;
    public IReadOnlyCollection<IRegion> AllRegions => _regions;

    private Regions(List<Region> regions)
    {
        _regions = regions;
    }

    internal static Regions InitializeRegions(IEnumerable<JsonRegions> regions)
    {
        var regionsList = new List<Region>();
        foreach (var region in regions)
        {
            if (string.IsNullOrWhiteSpace(region.InternalName) ||
                string.IsNullOrWhiteSpace(region.DisplayName))
                throw new InvalidOperationException("Region has invalid data");

            regionsList.Add(new Region(region.InternalName, region.DisplayName));
        }

        return new Regions(regionsList);
    }

    internal void InitializeLanguageOverrides(IEnumerable<JsonRegions> regions)
    {
        foreach (var region in regions)
        {
            var regionToOverride = _regions.FirstOrDefault(x => x.InternalName == region.InternalName);
            if (regionToOverride == null)
            {
                Log.Debug("Region {RegionName} not found in regions list", region.InternalName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(region.DisplayName))
            {
                Log.Warning("Region {RegionName} has invalid display name", region.InternalName);
                continue;
            }

            regionToOverride.DisplayName = region.DisplayName;
        }
    }

    private class Region : IRegion
    {
        public Region(string internalName, string displayName)
        {
            InternalName = internalName;
            DisplayName = displayName;
        }

        public string InternalName { get; internal set; }
        public string DisplayName { get; internal set; }
    }
}

public class Classes
{
}

public class Elements
{
}