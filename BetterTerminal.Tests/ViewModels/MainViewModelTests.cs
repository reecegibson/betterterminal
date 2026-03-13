using BetterTerminal.ViewModels;
using Xunit;

namespace BetterTerminal.Tests.ViewModels;

/// <summary>
/// Tests for MainViewModel session management logic.
/// No WPF UI involved — pure ViewModel/model layer.
/// </summary>
public class MainViewModelTests
{
    // ── Construction ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesExactlyOneSession()
    {
        var vm = new MainViewModel();
        Assert.Single(vm.Sessions);
    }

    [Fact]
    public void Constructor_SetsActiveSessionToFirstSession()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.ActiveSession);
        Assert.Same(vm.Sessions[0], vm.ActiveSession);
    }

    [Fact]
    public void Constructor_FirstSessionTitled_Terminal1()
    {
        var vm = new MainViewModel();
        Assert.Equal("Terminal 1", vm.Sessions[0].Title);
    }

    // ── AddSession ──────────────────────────────────────────────────────

    [Fact]
    public void AddSession_IncreasesCountByOne()
    {
        var vm = new MainViewModel();
        vm.AddSessionCommand.Execute(null);
        Assert.Equal(2, vm.Sessions.Count);
    }

    [Fact]
    public void AddSession_MakesNewSessionActive()
    {
        var vm = new MainViewModel();
        vm.AddSessionCommand.Execute(null);
        Assert.Same(vm.Sessions[1], vm.ActiveSession);
    }

    [Fact]
    public void AddSession_TitlesAreSequential()
    {
        var vm = new MainViewModel();
        vm.AddSessionCommand.Execute(null);
        vm.AddSessionCommand.Execute(null);
        Assert.Equal("Terminal 1", vm.Sessions[0].Title);
        Assert.Equal("Terminal 2", vm.Sessions[1].Title);
        Assert.Equal("Terminal 3", vm.Sessions[2].Title);
    }

    [Fact]
    public void AddSession_ShellIsNotEmpty()
    {
        var vm = new MainViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.Sessions[0].Shell));
    }

    // ── CloseSession ────────────────────────────────────────────────────

    [Fact]
    public void CloseSession_RemovesSessionFromCollection()
    {
        var vm = new MainViewModel();
        var s = vm.Sessions[0];
        vm.CloseSessionCommand.Execute(s);
        Assert.DoesNotContain(s, vm.Sessions);
    }

    [Fact]
    public void CloseSession_LastSession_ActiveBecomesNull()
    {
        var vm = new MainViewModel();
        vm.CloseSessionCommand.Execute(vm.Sessions[0]);
        Assert.Null(vm.ActiveSession);
    }

    [Fact]
    public void CloseSession_ActiveSession_SelectsNeighbor()
    {
        var vm = new MainViewModel();
        vm.AddSessionCommand.Execute(null);   // T1, T2
        var t1 = vm.Sessions[0];

        vm.ActiveSession = t1;
        vm.CloseSessionCommand.Execute(t1);   // close T1, T2 remains

        Assert.Single(vm.Sessions);
        Assert.NotNull(vm.ActiveSession);
        Assert.Same(vm.Sessions[0], vm.ActiveSession);
    }

    [Fact]
    public void CloseSession_NonActiveSession_ActiveUnchanged()
    {
        var vm = new MainViewModel();
        vm.AddSessionCommand.Execute(null);   // T1, T2
        var t1 = vm.Sessions[0];
        var t2 = vm.Sessions[1];

        vm.ActiveSession = t2;
        vm.CloseSessionCommand.Execute(t1);   // close T1 while T2 is active

        Assert.Same(t2, vm.ActiveSession);
    }

    [Fact]
    public void CloseSession_Null_DoesNotThrow()
    {
        var vm = new MainViewModel();
        var ex = Record.Exception(() => vm.CloseSessionCommand.Execute(null));
        Assert.Null(ex);
    }

    [Fact]
    public void CloseSession_LastActiveSession_OtherSessionAtEndSelected()
    {
        var vm = new MainViewModel();
        vm.AddSessionCommand.Execute(null);
        vm.AddSessionCommand.Execute(null);   // T1, T2, T3
        var t3 = vm.Sessions[2];

        vm.ActiveSession = t3;
        vm.CloseSessionCommand.Execute(t3);   // close last; should pick T2

        Assert.Equal(2, vm.Sessions.Count);
        Assert.Same(vm.Sessions[1], vm.ActiveSession); // T2, now at index 1
    }

    // ── RequestRename ───────────────────────────────────────────────────

    [Fact]
    public void RequestRename_FiresRenameRequestedEvent()
    {
        var vm = new MainViewModel();
        SessionViewModel? received = null;
        vm.RenameRequested += s => received = s;

        vm.RequestRenameCommand.Execute(vm.Sessions[0]);

        Assert.Same(vm.Sessions[0], received);
    }

    [Fact]
    public void RequestRename_Null_DoesNotFireEvent()
    {
        var vm = new MainViewModel();
        bool fired = false;
        vm.RenameRequested += _ => fired = true;

        vm.RequestRenameCommand.Execute(null);

        Assert.False(fired);
    }
}
