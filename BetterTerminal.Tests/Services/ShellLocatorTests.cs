using System.IO;
using BetterTerminal.Services;
using Xunit;

namespace BetterTerminal.Tests.Services;

public class ShellLocatorTests
{
    private static readonly string[] KnownShells =
        ["pwsh.exe", "powershell.exe", "cmd.exe"];

    [Fact]
    public void Resolve_ReturnsNonEmptyString()
    {
        string shell = ShellLocator.Resolve();
        Assert.False(string.IsNullOrWhiteSpace(shell));
    }

    [Fact]
    public void Resolve_ReturnsOneOfTheKnownShells()
    {
        string shell = ShellLocator.Resolve();
        string name  = Path.GetFileName(shell).ToLowerInvariant();
        Assert.Contains(name, KnownShells);
    }

    [Fact]
    public void Resolve_ReturnedPath_ExistsOnDisk_OrIsCmdExe()
    {
        // cmd.exe is resolved without a full path ("cmd.exe") — that's fine.
        // Everything else should be a resolvable full path.
        string shell = ShellLocator.Resolve();
        bool isCmdShorthand = shell.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase);
        Assert.True(isCmdShorthand || File.Exists(shell),
            $"Shell path does not exist on disk: {shell}");
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsSameResult()
    {
        // Deterministic — no randomness or side effects.
        Assert.Equal(ShellLocator.Resolve(), ShellLocator.Resolve());
    }
}
