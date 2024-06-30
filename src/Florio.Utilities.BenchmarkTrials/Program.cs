using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

using Florio.Utilities.BenchmarkTrials;

namespace Florio.Gutenberg.BenchmarkTrials
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = DefaultConfig.Instance;
            var summary = BenchmarkRunner.Run<FindReferencedWordsBenchmarks>(config, args);

            // Use this to select benchmarks from the console:
            // var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}