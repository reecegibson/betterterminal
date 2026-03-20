namespace BetterTerminal.Services;

/// <summary>
/// Extracts OSC 0/2 title sequences from raw terminal output.
/// Handles sequences that are split across ConPTY output chunks (fragmentation).
/// </summary>
public static class OscTitleParser
{
    /// <summary>
    /// Scans <paramref name="output"/> for OSC 0 or OSC 2 title sequences.
    /// Returns the <b>last</b> complete title found, or null if none.
    /// Sets <paramref name="trailingPartialStart"/> to the index of an unterminated
    /// trailing OSC sequence (-1 if none), so the caller can buffer the remainder
    /// and prepend it to the next chunk.
    /// </summary>
    public static string? ParseOscTitle(ReadOnlySpan<char> output, out int trailingPartialStart)
    {
        trailingPartialStart = -1;
        if (output.Length == 0) return null;

        string? lastTitle = null;
        int pos = 0;

        while (pos < output.Length)
        {
            int esc = output[pos..].IndexOf('\x1b');
            if (esc < 0) break;
            esc += pos; // absolute index

            // Need at least ESC ] <digit> ; to form an OSC title header
            if (esc + 3 >= output.Length)
            {
                // Partial header at end of chunk — could be start of an OSC sequence
                if (IsPartialOscHeader(output[esc..]))
                    trailingPartialStart = esc;
                break;
            }

            if (output[esc + 1] != ']' ||
                (output[esc + 2] != '0' && output[esc + 2] != '2') ||
                output[esc + 3] != ';')
            {
                pos = esc + 1;
                continue;
            }

            // We have a valid OSC 0/2 header — scan for terminator
            int titleStart = esc + 4;
            int bel = output[titleStart..].IndexOf('\x07');
            int stEsc = output[titleStart..].IndexOf('\x1b');

            // ST terminator: ESC followed by backslash
            int stEnd = -1;
            if (stEsc >= 0 && titleStart + stEsc + 1 < output.Length &&
                output[titleStart + stEsc + 1] == '\\')
            {
                stEnd = stEsc;
            }

            if (bel >= 0 && (stEnd < 0 || bel <= stEnd))
            {
                // BEL-terminated
                lastTitle = output.Slice(titleStart, bel).ToString();
                trailingPartialStart = -1;
                pos = titleStart + bel + 1;
            }
            else if (stEnd >= 0)
            {
                // ST-terminated
                lastTitle = output.Slice(titleStart, stEnd).ToString();
                trailingPartialStart = -1;
                pos = titleStart + stEnd + 2; // skip ESC and backslash
            }
            else
            {
                // No terminator found — this is a trailing partial
                trailingPartialStart = esc;
                break;
            }
        }

        return lastTitle;
    }

    /// <summary>
    /// Returns true if the span looks like the beginning of an OSC 0/2 title
    /// sequence that was truncated. Matches: ESC, ESC ], ESC ] 0, ESC ] 2,
    /// ESC ] 0 ;, ESC ] 2 ;, or ESC ] 0 ; title... (without terminator).
    /// </summary>
    private static bool IsPartialOscHeader(ReadOnlySpan<char> span)
    {
        if (span.Length == 0 || span[0] != '\x1b') return false;
        if (span.Length == 1) return true;  // just ESC
        if (span[1] != ']') return false;
        if (span.Length == 2) return true;  // ESC ]
        if (span[2] != '0' && span[2] != '2') return false;
        if (span.Length == 3) return true;  // ESC ] 0 or ESC ] 2
        // ESC ] 0 ; or ESC ] 2 ; (with or without partial title)
        return span[3] == ';';
    }
}
