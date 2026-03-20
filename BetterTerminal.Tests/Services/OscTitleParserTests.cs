using BetterTerminal.Services;
using Xunit;

namespace BetterTerminal.Tests.Services;

/// <summary>
/// Tests for OscTitleParser — verifies OSC 0/2 title extraction including
/// fragmentation across ConPTY output chunks.
/// </summary>
public class OscTitleParserTests
{
    // ── Basic extraction ─────────────────────────────────────────────────

    [Fact]
    public void BelTerminated_ReturnsTitle()
    {
        var input = "\x1b]0;My Title\x07".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("My Title", result);
        Assert.Equal(-1, partial);
    }

    [Fact]
    public void StTerminated_ReturnsTitle()
    {
        var input = "\x1b]0;My Title\x1b\\".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("My Title", result);
        Assert.Equal(-1, partial);
    }

    [Fact]
    public void Osc2_ReturnsTitle()
    {
        var input = "\x1b]2;Window Title\x07".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("Window Title", result);
        Assert.Equal(-1, partial);
    }

    // ── Embedded in larger output ────────────────────────────────────────

    [Fact]
    public void TitleEmbeddedInOutput_ReturnsTitle()
    {
        var input = "some text\x1b]0;Hello\x07more text".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("Hello", result);
        Assert.Equal(-1, partial);
    }

    // ── Multiple titles — last wins ──────────────────────────────────────

    [Fact]
    public void MultipleTitles_ReturnsLast()
    {
        var input = "\x1b]0;First\x07stuff\x1b]0;Second\x07".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("Second", result);
        Assert.Equal(-1, partial);
    }

    [Fact]
    public void MultipleTitles_MixedTerminators_ReturnsLast()
    {
        var input = "\x1b]0;First\x07\x1b]2;Second\x1b\\".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("Second", result);
        Assert.Equal(-1, partial);
    }

    // ── Fragmentation / partial sequences ────────────────────────────────

    [Fact]
    public void TrailingPartial_JustEsc_ReportsPartialStart()
    {
        var input = "output\x1b".AsSpan();
        OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal(6, partial);
    }

    [Fact]
    public void TrailingPartial_EscBracket_ReportsPartialStart()
    {
        var input = "output\x1b]".AsSpan();
        OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal(6, partial);
    }

    [Fact]
    public void TrailingPartial_EscBracketDigit_ReportsPartialStart()
    {
        var input = "output\x1b]0".AsSpan();
        OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal(6, partial);
    }

    [Fact]
    public void TrailingPartial_HeaderPlusTitleNoTerminator_ReportsPartialStart()
    {
        var input = "output\x1b]0;partial title".AsSpan();
        OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal(6, partial);
    }

    [Fact]
    public void Reassembly_AcrossChunks_ReturnsTitle()
    {
        // Simulate chunk 1 ending mid-sequence, chunk 2 completing it.
        var chunk1 = "text\x1b]0;Clau".AsSpan();
        var title1 = OscTitleParser.ParseOscTitle(chunk1, out int partial1);
        Assert.Null(title1);
        Assert.Equal(4, partial1);

        // Caller buffers chunk1[partial1..] = "\x1b]0;Clau"
        var buffered = chunk1[partial1..].ToString();
        var chunk2Combined = (buffered + "de Code\x07more output").AsSpan();
        var title2 = OscTitleParser.ParseOscTitle(chunk2Combined, out int partial2);
        Assert.Equal("Claude Code", title2);
        Assert.Equal(-1, partial2);
    }

    [Fact]
    public void CompleteTitleFollowedByPartial_ReturnsTitleAndReportsPartial()
    {
        var input = "\x1b]0;Done\x07text\x1b]0;Partial".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("Done", result);
        Assert.True(partial > 0);
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void EmptyInput_ReturnsNull()
    {
        var result = OscTitleParser.ParseOscTitle(ReadOnlySpan<char>.Empty, out int partial);
        Assert.Null(result);
        Assert.Equal(-1, partial);
    }

    [Fact]
    public void NoEscapeSequences_ReturnsNull()
    {
        var input = "just plain text with no escapes".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Null(result);
        Assert.Equal(-1, partial);
    }

    [Fact]
    public void NonTitleOsc_ReturnsNull()
    {
        // OSC 7 (working directory) should be ignored
        var input = "\x1b]7;file:///home/user\x07".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Null(result);
        Assert.Equal(-1, partial);
    }

    [Fact]
    public void EmojiInTitle_ReturnsTitle()
    {
        var input = "\x1b]0;\U0001f680 Deploying...\x07".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("\U0001f680 Deploying...", result);
        Assert.Equal(-1, partial);
    }

    [Fact]
    public void EmptyTitle_ReturnsEmptyString()
    {
        var input = "\x1b]0;\x07".AsSpan();
        var result = OscTitleParser.ParseOscTitle(input, out int partial);
        Assert.Equal("", result);
        Assert.Equal(-1, partial);
    }
}
