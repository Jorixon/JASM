using BenchmarkDotNet.Attributes;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.SkinMod;

namespace JASM.Benchmark;

[SimpleJob(invocationCount: 10000)]
[GcServer(false)]
public class CreateSkinMod_Benchmark
{
    private DirectoryInfo ModFolder = null!;

    [GlobalSetup]
    public void SetupFolders()
    {
        ModFolder = new DirectoryInfo(Values.TestModFolderPath);
        Console.WriteLine("ModFolder: " + ModFolder.FullName);
    }


    [Benchmark]
    public async Task<ISkinMod> CreateModAsync()
    {
        return await SkinMod.CreateModAsync(ModFolder).ConfigureAwait(false);
    }
}

[SimpleJob(invocationCount: 10000)]
[GcServer(false)]
public class ReadSkinModSettings_Benchmark
{
    private DirectoryInfo ModFolder = null!;
    private ISkinMod _skinMod = null!;

    [GlobalSetup]
    public void SetupFolders()
    {
        ModFolder = new DirectoryInfo(Values.TestModFolderPath);
        Console.WriteLine("ModFolder: " + ModFolder.FullName);
    }


    [IterationSetup]
    public void Setup()
    {
        _skinMod = SkinMod.CreateModAsync(ModFolder).GetAwaiter().GetResult();
    }


    [Benchmark]
    public async Task<ModSettings> ReadModSettingsAsync()
    {
        return await _skinMod.Settings.ReadSettingsAsync(useCache: false).ConfigureAwait(false);
    }
}