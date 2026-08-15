using BenchmarkDotNet.Running;

namespace LibTmux.Benchmarks;

internal static class Program
{
    private static void Main(string[] arguments) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(arguments);
}
