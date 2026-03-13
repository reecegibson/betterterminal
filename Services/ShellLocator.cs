using System.IO;

namespace BetterTerminal.Services;

/// <summary>
/// Picks the best available interactive shell on this machine.
/// Priority: PowerShell 7 (pwsh) → Windows PowerShell → cmd.
/// </summary>
public static class ShellLocator
{
    public static string Resolve()
    {
        // 1. PowerShell 7 — look on PATH
        string? pwsh = FindOnPath("pwsh.exe");
        if (pwsh is not null) return pwsh;

        // 2. Windows PowerShell — fixed location
        string ps1 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");
        if (File.Exists(ps1)) return ps1;

        // 3. cmd.exe — always present
        return "cmd.exe";
    }

    private static string? FindOnPath(string exe)
    {
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar is null) return null;

        foreach (string dir in pathVar.Split(';'))
        {
            string full = Path.Combine(dir.Trim(), exe);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
