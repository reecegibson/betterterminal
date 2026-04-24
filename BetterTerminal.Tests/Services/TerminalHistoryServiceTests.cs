using BetterTerminal.Services;
using Xunit;

namespace BetterTerminal.Tests.Services;

public class TerminalHistoryServiceTests
{
    // ── Constant correctness ──────────────────────────────────────────

    [Fact]
    public void ClearScrollbackSequence_StartsWithEscCsi()
    {
        // Every CSI sequence must begin with ESC (0x1B) followed by '['
        Assert.StartsWith("\x1b[", TerminalHistoryService.ClearScrollbackSequence);
    }

    [Fact]
    public void ClearScrollbackSequence_IsExactlyEscBracket3J()
    {
        // CSI 3 J = "Erase Saved Lines" — the xterm sequence for clearing scrollback.
        Assert.Equal("\x1b[3J", TerminalHistoryService.ClearScrollbackSequence);
    }

    [Fact]
    public void ClearScrollbackSequence_HasCorrectLength()
    {
        // ESC [ 3 J = 4 characters
        Assert.Equal(4, TerminalHistoryService.ClearScrollbackSequence.Length);
    }

    [Fact]
    public void ClearScreenSequence_IsExactlyEscBracket2J()
    {
        // CSI 2 J = "Erase in Display" — clear the visible screen.
        Assert.Equal("\x1b[2J", TerminalHistoryService.ClearScreenSequence);
    }

    [Fact]
    public void CursorHomeSequence_IsExactlyEscBracketH()
    {
        // CSI H = "Cursor Position" with default args (1,1) = home.
        Assert.Equal("\x1b[H", TerminalHistoryService.CursorHomeSequence);
    }

    // ── BuildClearSequence — scrollback only (default) ────────────────

    [Fact]
    public void BuildClearSequence_Default_ReturnsClearScrollbackOnly()
    {
        var seq = TerminalHistoryService.BuildClearSequence();
        Assert.Equal(TerminalHistoryService.ClearScrollbackSequence, seq);
    }

    [Fact]
    public void BuildClearSequence_ScrollbackOnly_DoesNotContainScreenClear()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: false);
        Assert.DoesNotContain("\x1b[2J", seq);
    }

    [Fact]
    public void BuildClearSequence_ScrollbackOnly_DoesNotContainCursorHome()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: false);
        Assert.DoesNotContain("\x1b[H", seq);
    }

    [Fact]
    public void BuildClearSequence_ScrollbackOnly_DoesNotContainNewlines()
    {
        // The sequence must not inject visible text or newlines into the terminal.
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: false);
        Assert.DoesNotContain("\r", seq);
        Assert.DoesNotContain("\n", seq);
    }

    [Fact]
    public void BuildClearSequence_ScrollbackOnly_ContainsExactlyOneEsc()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: false);
        int escCount = seq.Count(c => c == '\x1b');
        Assert.Equal(1, escCount);
    }

    // ── BuildClearSequence — full clear (clearVisible: true) ──────────

    [Fact]
    public void BuildClearSequence_ClearVisible_ContainsScrollbackClear()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: true);
        Assert.Contains(TerminalHistoryService.ClearScrollbackSequence, seq);
    }

    [Fact]
    public void BuildClearSequence_ClearVisible_ContainsScreenClear()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: true);
        Assert.Contains(TerminalHistoryService.ClearScreenSequence, seq);
    }

    [Fact]
    public void BuildClearSequence_ClearVisible_ContainsCursorHome()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: true);
        Assert.Contains(TerminalHistoryService.CursorHomeSequence, seq);
    }

    [Fact]
    public void BuildClearSequence_ClearVisible_ScrollbackClearedFirst()
    {
        // Scrollback should be purged before the screen is cleared,
        // so the renderer drops old lines before wiping visible ones.
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: true);
        int scrollbackIdx = seq.IndexOf(TerminalHistoryService.ClearScrollbackSequence, StringComparison.Ordinal);
        int screenIdx = seq.IndexOf(TerminalHistoryService.ClearScreenSequence, StringComparison.Ordinal);
        Assert.True(scrollbackIdx < screenIdx,
            "ClearScrollback (CSI 3 J) must appear before ClearScreen (CSI 2 J)");
    }

    [Fact]
    public void BuildClearSequence_ClearVisible_CursorHomedLast()
    {
        // Cursor home should come after both clears so the cursor lands at (1,1)
        // on the freshly cleared screen.
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: true);
        int homeIdx = seq.IndexOf(TerminalHistoryService.CursorHomeSequence, StringComparison.Ordinal);
        int screenIdx = seq.IndexOf(TerminalHistoryService.ClearScreenSequence, StringComparison.Ordinal);
        Assert.True(homeIdx > screenIdx,
            "CursorHome (CSI H) must appear after ClearScreen (CSI 2 J)");
    }

    [Fact]
    public void BuildClearSequence_ClearVisible_ContainsExactlyThreeEscSequences()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: true);
        int escCount = seq.Count(c => c == '\x1b');
        Assert.Equal(3, escCount);
    }

    [Fact]
    public void BuildClearSequence_ClearVisible_DoesNotContainNewlines()
    {
        var seq = TerminalHistoryService.BuildClearSequence(clearVisible: true);
        Assert.DoesNotContain("\r", seq);
        Assert.DoesNotContain("\n", seq);
    }

    // ── VT sequence format validation ─────────────────────────────────

    [Fact]
    public void AllSequences_UseCorrectEscCharacter()
    {
        // ESC must be byte 0x1B (27 decimal), not a printable character.
        Assert.Equal(0x1B, TerminalHistoryService.ClearScrollbackSequence[0]);
        Assert.Equal(0x1B, TerminalHistoryService.ClearScreenSequence[0]);
        Assert.Equal(0x1B, TerminalHistoryService.CursorHomeSequence[0]);
    }

    [Fact]
    public void AllSequences_FollowEscWithOpenBracket()
    {
        // CSI = ESC + '[' — Control Sequence Introducer.
        Assert.Equal('[', TerminalHistoryService.ClearScrollbackSequence[1]);
        Assert.Equal('[', TerminalHistoryService.ClearScreenSequence[1]);
        Assert.Equal('[', TerminalHistoryService.CursorHomeSequence[1]);
    }

    [Fact]
    public void ClearScrollback_UsesParameterThree()
    {
        // The '3' in CSI 3 J means "erase saved lines" (scrollback).
        // Parameter '0' = below cursor, '1' = above cursor, '2' = all visible,
        // '3' = saved lines (scrollback).  Using the wrong number would
        // clear the wrong thing.
        Assert.Equal('3', TerminalHistoryService.ClearScrollbackSequence[2]);
        Assert.Equal('J', TerminalHistoryService.ClearScrollbackSequence[3]);
    }

    [Fact]
    public void ClearScreen_UsesParameterTwo()
    {
        // '2' in CSI 2 J means "erase entire display".
        Assert.Equal('2', TerminalHistoryService.ClearScreenSequence[2]);
        Assert.Equal('J', TerminalHistoryService.ClearScreenSequence[3]);
    }
}
