namespace BetterTerminal.Services;

/// <summary>
/// Geometry for a borderless (<c>WindowStyle="None"</c>) window.
///
/// Such a window has no OS frame, so Windows maximizes it to the monitor's full
/// pixel rectangle — on top of the taskbar. The fix is to answer
/// <c>WM_GETMINMAXINFO</c> with the monitor's <i>work area</i> instead. This class
/// holds the pure arithmetic for that answer so it can be unit-tested without an HWND;
/// <c>MainWindow</c> supplies the rectangles from <c>GetMonitorInfo</c>.
/// </summary>
public static class WindowPlacementService
{
    /// <summary>A Win32-style rectangle in physical screen pixels (may be negative on secondary monitors).</summary>
    public readonly record struct Bounds(int Left, int Top, int Right, int Bottom)
    {
        public int Width  => Right - Left;
        public int Height => Bottom - Top;
        public bool IsEmpty => Width <= 0 || Height <= 0;
    }

    /// <summary>
    /// A maximized window rectangle. <see cref="X"/>/<see cref="Y"/> are relative to the
    /// monitor's top-left corner, because that is what <c>MINMAXINFO.ptMaxPosition</c> expects.
    /// </summary>
    public readonly record struct Placement(int X, int Y, int Width, int Height);

    /// <summary>
    /// Returns where a maximized window should sit so that it fills <paramref name="work"/>
    /// (the monitor minus the taskbar and any other appbars) rather than <paramref name="monitor"/>.
    /// Falls back to <paramref name="monitor"/> if the work area is missing or degenerate,
    /// which keeps the window usable rather than zero-sized.
    /// </summary>
    public static Placement MaximizedPlacement(Bounds monitor, Bounds work)
    {
        var target = work.IsEmpty ? monitor : work;

        return new Placement(
            X:      target.Left - monitor.Left,
            Y:      target.Top  - monitor.Top,
            Width:  target.Width,
            Height: target.Height);
    }
}
