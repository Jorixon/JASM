using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Services;

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