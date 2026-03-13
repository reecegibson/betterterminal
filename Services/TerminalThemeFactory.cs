using System.Windows.Media;
using EasyWindowsTerminalControl;
using Microsoft.Terminal.Wpf;

namespace BetterTerminal.Services;

/// <summary>
/// Builds a TerminalTheme that matches the app's dark palette.
/// Uses the "Campbell" colour scheme from Windows Terminal.
/// </summary>
public static class TerminalThemeFactory
{
    private static uint C(byte r, byte g, byte b)
        => EasyTerminalControl.ColorToVal(Color.FromRgb(r, g, b));

    public static TerminalTheme CreateDark() => new()
    {
        DefaultBackground          = C(0x0C, 0x0C, 0x0C),
        DefaultForeground          = C(0xCC, 0xCC, 0xCC),
        DefaultSelectionBackground = C(0x00, 0x7A, 0xCC),
        CursorStyle                = 0,   // 0 = bar / vintage block depending on terminal version
        ColorTable = new uint[]
        {
            // Campbell dark — standard 16 ANSI slots
            C(0x0C, 0x0C, 0x0C), //  0 Black
            C(0xC5, 0x0F, 0x1F), //  1 DarkRed
            C(0x13, 0xA1, 0x0E), //  2 DarkGreen
            C(0xC1, 0x9C, 0x00), //  3 DarkYellow
            C(0x00, 0x37, 0xDA), //  4 DarkBlue
            C(0x88, 0x17, 0x98), //  5 DarkMagenta
            C(0x3A, 0x96, 0xDD), //  6 DarkCyan
            C(0xCC, 0xCC, 0xCC), //  7 LightGray
            C(0x76, 0x76, 0x76), //  8 DarkGray
            C(0xE7, 0x48, 0x56), //  9 Red
            C(0x16, 0xC6, 0x0C), // 10 Green
            C(0xF9, 0xF1, 0xA5), // 11 Yellow
            C(0x3B, 0x78, 0xFF), // 12 Blue
            C(0xB4, 0x00, 0x9E), // 13 Magenta
            C(0x61, 0xD6, 0xD6), // 14 Cyan
            C(0xF2, 0xF2, 0xF2), // 15 White
        },
    };
}
