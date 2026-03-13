using System.Collections.ObjectModel;
using BetterTerminal.Models;
using BetterTerminal.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BetterTerminal.ViewModels;

/// <summary>
/// Root view-model: owns the session list and the currently-active session.
/// The view drives selection via the two-way SelectedItem binding on the
/// sidebar ListBox; this VM is the single source of truth for ActiveSession.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // Bound to ListBox.SelectedItem (TwoWay) so clicking a row updates this.
    [ObservableProperty]
    private SessionViewModel? _activeSession;

    public ObservableCollection<SessionViewModel> Sessions { get; } = new();

    /// <summary>
    /// The view subscribes to this and shows its rename dialog,
    /// then writes the new title back to the session.
    /// </summary>
    public event Action<SessionViewModel>? RenameRequested;

    public MainViewModel() => AddSession();

    // ── Commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddSession()
    {
        var vm = new SessionViewModel(new Session
        {
            Title = $"Terminal {Sessions.Count + 1}",
            Shell = ShellLocator.Resolve(),
        });
        Sessions.Add(vm);
        ActiveSession = vm;
    }

    [RelayCommand]
    private void CloseSession(SessionViewModel? session)
    {
        if (session is null) return;

        int idx = Sessions.IndexOf(session);
        Sessions.Remove(session);

        if (Sessions.Count == 0)
        {
            ActiveSession = null;
        }
        else if (ActiveSession == session)
        {
            // Move to the session that now occupies the same slot, or the last one.
            ActiveSession = Sessions[Math.Clamp(idx, 0, Sessions.Count - 1)];
        }
    }

    [RelayCommand]
    private void RequestRename(SessionViewModel? session)
    {
        if (session is not null)
            RenameRequested?.Invoke(session);
    }
}
