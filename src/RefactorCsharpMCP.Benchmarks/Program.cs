using BenchmarkDotNet.Running;

namespace RefactorCsharpMCP.Benchmarks;

/// <summary>
/// BenchmarkDotNet runner for RefactorCsharpMCP performance benchmarks.
///
/// Usage:
///   dotnet run -c Release                    # Run all benchmarks
///   dotnet run -c Release --filter *Extract* # Run only Extract* benchmarks
///   dotnet run -c Release --list flat        # List available benchmarks
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
