using BetterTerminal.Models;
using BetterTerminal.ViewModels;
using Xunit;

namespace BetterTerminal.Tests.ViewModels;

public class SessionViewModelTests
{
    private static SessionViewModel Make(string title = "My Session", string shell = "cmd.exe")
        => new(new Session { Title = title, Shell = shell });

    // ── Construction ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsTitle()
    {
        var vm = Make("Hello");
        Assert.Equal("Hello", vm.Title);
    }

    [Fact]
    public void Constructor_SetsShell()
    {
        var vm = Make(shell: "pwsh.exe");
        Assert.Equal("pwsh.exe", vm.Shell);
    }

    [Fact]
    public void Constructor_IdMatchesModel()
    {
        var model = new Session { Title = "T" };
        var vm = new SessionViewModel(model);
        Assert.Equal(model.Id, vm.Id);
    }

    // ── Title mutation ──────────────────────────────────────────────────

    [Fact]
    public void Title_CanBeChanged()
    {
        var vm = Make("Old");
        vm.Title = "New";
        Assert.Equal("New", vm.Title);
    }

    [Fact]
    public void Title_Change_RaisesPropertyChanged()
    {
        var vm = Make("Old");
        bool fired = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SessionViewModel.Title))
                fired = true;
        };

        vm.Title = "New";

        Assert.True(fired);
    }

    [Fact]
    public void Title_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = Make("Same");
        int count = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SessionViewModel.Title))
                count++;
        };

        vm.Title = "Same";   // no change

        Assert.Equal(0, count);
    }

    // ── ToModel ─────────────────────────────────────────────────────────

    [Fact]
    public void ToModel_PreservesId()
    {
        var model = new Session { Title = "T" };
        var vm = new SessionViewModel(model);
        Assert.Equal(model.Id, vm.ToModel().Id);
    }

    [Fact]
    public void ToModel_ReflectsRenamedTitle()
    {
        var vm = Make("Original");
        vm.Title = "Renamed";
        Assert.Equal("Renamed", vm.ToModel().Title);
    }

    [Fact]
    public void ToModel_ReflectsShell()
    {
        var vm = Make(shell: "pwsh.exe");
        Assert.Equal("pwsh.exe", vm.ToModel().Shell);
    }
}
