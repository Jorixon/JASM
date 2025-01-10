using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Services;
using Serilog;

namespace JASM.Benchmark;

[SimpleJob(RunStrategy.ColdStart, iterationCount: 100)]
[GcServer(false)]
public class SkinManagerService_Benchmarks
{
    private DirectoryInfo TmpFolder = null!;
    private ISkinManagerService _skinManagerService = null!;
    private string assetDir = Helpers.GetGamesFolder("Genshin").FullName;


    [GlobalSetup]
    public void SetupFolders()
    {
        TmpFolder = Helpers.GetTmpFolder();


        Console.WriteLine("AssetDir: " + assetDir);
        Console.WriteLine("ActiveModsFolderPath: " + Values.ActiveModsFolderPath);
        Console.WriteLine("ThreeMigotoRootfolder: " + Values.ThreeMigotoRootfolder);
        Console.WriteLine("TmpFolder: " + TmpFolder.FullName);
    }

    [IterationSetup]
    public void Setup()
    {
        var logger = new MockLogger();

        var gameService = new GameService(logger, new MockLocalizer());

        gameService.InitializeAsync(new InitializationOptions
        {
            AssetsDirectory = assetDir,
            LocalSettingsDirectory = TmpFolder.FullName
        }).GetAwaiter().GetResult();

        var crawlerService = new ModCrawlerService(logger, gameService);

        _skinManagerService = new SkinManagerService(gameService, logger, crawlerService);
    }


    //[Benchmark(Baseline = true)]
    //public async Task BaseInitializeAsync()
    //{
    //    await _skinManagerService.InitializeAsync(Values.ActiveModsFolderPath, null, Values.ThreeMigotoRootfolder, true).ConfigureAwait(false);
    //}

    [Benchmark]
    public async Task InitializeAsync()
    {
        await _skinManagerService.InitializeAsync(Values.ActiveModsFolderPath, null, Values.ThreeMigotoRootfolder).ConfigureAwait(false);
    }

    [IterationCleanup]
    public void Cleanup()
    {
        TmpFolder.Delete(true);
    }
}

[SimpleJob(RunStrategy.ColdStart, iterationCount: 10, invocationCount: 10)]
[GcServer(false)]
public class SkinManagerService_Benchmarks_Alternative
{
    private DirectoryInfo TmpFolder = null!;
    private ISkinManagerService _skinManagerService = null!;
    private string assetDir = Helpers.GetGamesFolder("Genshin").FullName;

    private ILogger _logger = new MockLogger();
    private GameService _gameService = null!;
    private ModCrawlerService _crawlerService = null!;


    [GlobalSetup]
    public void SetupFolders()
    {
        TmpFolder = Helpers.GetTmpFolder();


        Console.WriteLine("AssetDir: " + assetDir);
        Console.WriteLine("ActiveModsFolderPath: " + Values.ActiveModsFolderPath);
        Console.WriteLine("ThreeMigotoRootfolder: " + Values.ThreeMigotoRootfolder);
        Console.WriteLine("TmpFolder: " + TmpFolder.FullName);
    }

    [IterationSetup]
    public void Setup()
    {
        _gameService = new GameService(_logger, new MockLocalizer());

        _gameService.InitializeAsync(new InitializationOptions
        {
            AssetsDirectory = assetDir,
            LocalSettingsDirectory = TmpFolder.FullName
        }).GetAwaiter().GetResult();

        _crawlerService = new ModCrawlerService(_logger, _gameService);
    }


    //[Benchmark(Baseline = true)]
    //public async Task BaseInitializeAsync()
    //{
    //    _skinManagerService = new SkinManagerService(_gameService, _logger, _crawlerService);
    //    await _skinManagerService.InitializeAsync(Values.ActiveModsFolderPath, null, Values.ThreeMigotoRootfolder, true).ConfigureAwait(false);
    //    _skinManagerService.Dispose();
    //}

    [Benchmark]
    public async Task InitializeAsync()
    {
        _skinManagerService = new SkinManagerService(_gameService, _logger, _crawlerService);
        await _skinManagerService.InitializeAsync(Values.ActiveModsFolderPath, null, Values.ThreeMigotoRootfolder).ConfigureAwait(false);
        _skinManagerService.Dispose();
    }

    [IterationCleanup]
    public void Cleanup()
    {
        TmpFolder.Delete(true);
    }
}