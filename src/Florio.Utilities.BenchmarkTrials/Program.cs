using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

using Florio.Utilities.BenchmarkTrials;



var config = DefaultConfig.Instance;

var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
