namespace BetterTerminal.Services;

/// <summary>
/// Builds VT escape sequences for clearing terminal scrollback and screen content.
/// </summary>
public static class TerminalHistoryService
{
    // ESC character (0x1B) starts all VT escape sequences.
    private const char Esc = '\x1b';

    /// <summary>
    /// CSI 3 J — "Erase Saved Lines": clears the scrollback buffer above the viewport
    /// while leaving visible content untouched.
    /// </summary>
    public const string ClearScrollbackSequence = "\x1b[3J";

    /// <summary>
    /// CSI 2 J — "Erase in Display": clears the entire visible screen.
    /// </summary>
    public const string ClearScreenSequence = "\x1b[2J";

    /// <summary>
    /// CSI H — "Cursor Position": moves cursor to row 1, column 1 (home).
    /// </summary>
    public const string CursorHomeSequence = "\x1b[H";

    /// <summary>
    /// Builds the VT escape sequence string to clear the terminal.
    /// When <paramref name="clearVisible"/> is false (default), only the scrollback
    /// buffer is purged — visible content stays. When true, the visible screen
    /// is also wiped and the cursor is homed.
    /// </summary>
    public static string BuildClearSequence(bool clearVisible = false)
    {
        if (clearVisible)
            return ClearScrollbackSequence + ClearScreenSequence + CursorHomeSequence;

        return ClearScrollbackSequence;
    }
}
