using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace MyApp.Benchmarks;

/// <summary>
/// BenchmarkDotNet 0.15.8 does not know the net11.0 preview moniker, so out-of-process runs fail validation: everything runs in-process.
/// </summary>
internal static class BenchmarkConfig
{
    public static IConfig Create(bool smoke)
    {
        var job = (smoke ? Job.ShortRun : Job.Default).WithToolchain(InProcessEmitToolchain.Instance);

        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job)
            .AddDiagnoser(MemoryDiagnoser.Default);
    }
}
