using BenchmarkDotNet.Attributes;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;

namespace JASM.Benchmark.Benchmarks;

[SimpleJob(iterationCount: 5)]
public class Testing_Benchmark
{
    private IGameService _gameService = null!;
    private ISkinManagerService _skinManagerService = null!;
}