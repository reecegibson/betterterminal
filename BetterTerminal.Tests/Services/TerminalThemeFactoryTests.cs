using BetterTerminal.Services;
using Xunit;

namespace BetterTerminal.Tests.Services;

/// <summary>
/// Tests for TerminalThemeFactory — verifies the dark theme is structurally
/// valid and consistent without depending on specific COLORREF maths.
/// </summary>
public class TerminalThemeFactoryTests
{
    // ── ColorTable ──────────────────────────────────────────────────────

    [Fact]
    public void CreateDark_ColorTable_HasSixteenEntries()
    {
        var theme = TerminalThemeFactory.CreateDark();
        Assert.Equal(16, theme.ColorTable.Length);
    }

    [Fact]
    public void CreateDark_ColorTable_NoEntryIsZero()
    {
        // All 16 Campbell slots use non-black colours (index 0 is #0C0C0C,
        // not #000000), so every COLORREF should be non-zero.
        var theme = TerminalThemeFactory.CreateDark();
        for (int i = 0; i < theme.ColorTable.Length; i++)
            Assert.True(theme.ColorTable[i] != 0, $"ColorTable[{i}] is zero");
    }

    [Fact]
    public void CreateDark_ColorTable_AllEntriesDistinct()
    {
        // The 16 Campbell colours should all be unique.
        var theme = TerminalThemeFactory.CreateDark();
        var distinct = theme.ColorTable.Distinct().Count();
        Assert.Equal(16, distinct);
    }

    // ── Background / Foreground ─────────────────────────────────────────

    [Fact]
    public void CreateDark_Background_IsNonZero()
    {
        var theme = TerminalThemeFactory.CreateDark();
        Assert.NotEqual(0u, theme.DefaultBackground);
    }

    [Fact]
    public void CreateDark_Foreground_IsNonZero()
    {
        var theme = TerminalThemeFactory.CreateDark();
        Assert.NotEqual(0u, theme.DefaultForeground);
    }

    [Fact]
    public void CreateDark_SelectionBackground_IsNonZero()
    {
        var theme = TerminalThemeFactory.CreateDark();
        Assert.NotEqual(0u, theme.DefaultSelectionBackground);
    }

    [Fact]
    public void CreateDark_Background_DifferentFromForeground()
    {
        // Dark background and light foreground must differ for readable text.
        var theme = TerminalThemeFactory.CreateDark();
        Assert.NotEqual(theme.DefaultBackground, theme.DefaultForeground);
    }

    [Fact]
    public void CreateDark_Background_LessThanForeground()
    {
        // Dark background COLORREF < light foreground COLORREF because each
        // channel of #1E1E1E is smaller than #CCCCCC.
        var theme = TerminalThemeFactory.CreateDark();
        Assert.True(theme.DefaultBackground < theme.DefaultForeground,
            "Background should be darker (lower COLORREF) than foreground");
    }

    // ── Determinism ─────────────────────────────────────────────────────

    [Fact]
    public void CreateDark_CalledTwice_ReturnsSameValues()
    {
        var a = TerminalThemeFactory.CreateDark();
        var b = TerminalThemeFactory.CreateDark();

        Assert.Equal(a.DefaultBackground,          b.DefaultBackground);
        Assert.Equal(a.DefaultForeground,          b.DefaultForeground);
        Assert.Equal(a.DefaultSelectionBackground, b.DefaultSelectionBackground);
        Assert.Equal(a.CursorStyle,                b.CursorStyle);

        for (int i = 0; i < 16; i++)
            Assert.Equal(a.ColorTable[i], b.ColorTable[i]);
    }
}
