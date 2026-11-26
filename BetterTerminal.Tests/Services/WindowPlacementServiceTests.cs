using BetterTerminal.Services;
using Xunit;
using Bounds = BetterTerminal.Services.WindowPlacementService.Bounds;

namespace BetterTerminal.Tests.Services;

public class WindowPlacementServiceTests
{
    // 1920x1080 monitor at the origin, 40 px taskbar along the bottom.
    private static readonly Bounds PrimaryMonitor = new(0, 0, 1920, 1080);
    private static readonly Bounds PrimaryWork    = new(0, 0, 1920, 1040);

    [Fact]
    public void BottomTaskbar_ShrinksHeight()
    {
        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, PrimaryWork);

        Assert.Equal(1040, p.Height);
        Assert.Equal(1920, p.Width);
    }

    [Fact]
    public void BottomTaskbar_KeepsWindowAtMonitorOrigin()
    {
        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, PrimaryWork);

        Assert.Equal(0, p.X);
        Assert.Equal(0, p.Y);
    }

    [Fact]
    public void TopTaskbar_OffsetsWindowDown()
    {
        var work = new Bounds(0, 40, 1920, 1080);

        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, work);

        Assert.Equal(0, p.X);
        Assert.Equal(40, p.Y);
        Assert.Equal(1920, p.Width);
        Assert.Equal(1040, p.Height);
    }

    [Fact]
    public void LeftTaskbar_OffsetsWindowRightAndNarrows()
    {
        var work = new Bounds(60, 0, 1920, 1080);

        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, work);

        Assert.Equal(60, p.X);
        Assert.Equal(0, p.Y);
        Assert.Equal(1860, p.Width);
        Assert.Equal(1080, p.Height);
    }

    [Fact]
    public void RightTaskbar_NarrowsWithoutOffsettingPosition()
    {
        var work = new Bounds(0, 0, 1860, 1080);

        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, work);

        Assert.Equal(0, p.X);      // nothing reserved on the left edge
        Assert.Equal(0, p.Y);
        Assert.Equal(1860, p.Width);
        Assert.Equal(1080, p.Height);
    }

    [Fact]
    public void AutoHideTaskbar_FillsWholeMonitor()
    {
        // An auto-hiding taskbar reserves no space, so rcWork == rcMonitor and a
        // maximized window legitimately covers the screen — same as any native app.
        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, PrimaryMonitor);

        Assert.Equal(1080, p.Height);
    }

    [Fact]
    public void NoTaskbar_FillsWholeMonitor()
    {
        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, PrimaryMonitor);

        Assert.Equal(new WindowPlacementService.Placement(0, 0, 1920, 1080), p);
    }

    [Fact]
    public void SecondaryMonitorWithNegativeCoordinates_PositionIsMonitorRelative()
    {
        // Monitor to the left of (and above) the primary one — Win32 coords go negative.
        var monitor = new Bounds(-1920, -200, 0, 880);
        var work    = new Bounds(-1920, -200, 0, 840);

        var p = WindowPlacementService.MaximizedPlacement(monitor, work);

        // ptMaxPosition is relative to the monitor, so an unoccupied top-left stays 0,0.
        Assert.Equal(0, p.X);
        Assert.Equal(0, p.Y);
        Assert.Equal(1920, p.Width);
        Assert.Equal(1040, p.Height);
    }

    [Fact]
    public void SecondaryMonitorWithTaskbar_PositionExcludesMonitorOffset()
    {
        // Monitor to the right of the primary one, taskbar down its left edge.
        var monitor = new Bounds(1920, 0, 3840, 1080);
        var work    = new Bounds(1980, 0, 3840, 1040);

        var p = WindowPlacementService.MaximizedPlacement(monitor, work);

        Assert.Equal(60, p.X);     // not 1980 — the monitor origin is subtracted
        Assert.Equal(0, p.Y);
        Assert.Equal(1860, p.Width);
        Assert.Equal(1040, p.Height);
    }

    [Fact]
    public void EmptyWorkArea_FallsBackToMonitor()
    {
        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, new Bounds(0, 0, 0, 0));

        Assert.Equal(new WindowPlacementService.Placement(0, 0, 1920, 1080), p);
    }

    [Fact]
    public void InvertedWorkArea_FallsBackToMonitor()
    {
        var inverted = new Bounds(1920, 1080, 0, 0);

        var p = WindowPlacementService.MaximizedPlacement(PrimaryMonitor, inverted);

        Assert.Equal(new WindowPlacementService.Placement(0, 0, 1920, 1080), p);
    }

    [Fact]
    public void BoundsExposeWidthAndHeight()
    {
        var b = new Bounds(10, 20, 110, 220);

        Assert.Equal(100, b.Width);
        Assert.Equal(200, b.Height);
        Assert.False(b.IsEmpty);
    }
}
