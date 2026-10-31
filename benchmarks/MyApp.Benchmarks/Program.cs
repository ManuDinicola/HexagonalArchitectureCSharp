using BenchmarkDotNet.Running;
using MyApp.Benchmarks;

// --smoke runs a short job for a quick check; without it the default job gives the final numbers.
var smoke = args.Contains("--smoke", StringComparer.Ordinal);
var rest = args.Where(a => !string.Equals(a, "--smoke", StringComparison.Ordinal)).ToArray();

BenchmarkSwitcher.FromAssembly(typeof(BenchmarkConfig).Assembly).Run(rest, BenchmarkConfig.Create(smoke));
