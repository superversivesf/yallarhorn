namespace Yallarhorn.Tests;

using Xunit;

/// <summary>
/// Collection definition for tests that capture Console.Out.
/// Prevents parallel execution to avoid test pollution.
/// </summary>
[CollectionDefinition("ConsoleOutput", DisableParallelization = true)]
public class ConsoleOutputCollection
{
    // This class is never instantiated. It's just a marker for xUnit.
}