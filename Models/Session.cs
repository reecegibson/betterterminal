namespace BetterTerminal.Models;

/// <summary>
/// Persisted data for a single terminal session.
/// Serialised to JSON by SessionStore.
/// </summary>
public class Session
{
    public Guid    Id               { get; init; } = Guid.NewGuid();
    public string  Title            { get; set;  } = "Terminal";
    public string  Shell            { get; set;  } = string.Empty;
    public string? WorkingDirectory { get; set;  }
}
