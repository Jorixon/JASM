// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using JASM.Benchmark;

var config = DefaultConfig.Instance;
var summary = BenchmarkRunner.Run<SkinManagerService_Benchmarks>(config, args);