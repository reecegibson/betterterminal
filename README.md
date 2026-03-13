# BetterTerminal

A modern Windows terminal emulator built with WPF and real ConPTY sessions. Custom chrome, multi-tab sidebar, and a VS Code-inspired dark palette.

![.NET 8](https://img.shields.io/badge/.NET-8.0-blueviolet) ![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue) ![WPF](https://img.shields.io/badge/UI-WPF-informational)

---

## Features

- **Custom title bar** — borderless window with Windows 11 rounded corners (DWM), Minimize / Maximize / Close buttons
- **Session sidebar** — create, close, and rename terminal sessions; drag-to-reorder with visual drop indicator
- **Real ConPTY sessions** — each tab runs a full pseudo-console via `EasyWindowsTerminalControl`; shells keep running in the background when switching tabs
- **Auto tab titles** — OSC 0 / OSC 2 escape sequences update the sidebar tab name in real time
- **Activity indicator** — sidebar entry pulses when a session is producing output (idle debounce 500 ms)
- **Campbell dark theme** — Windows Terminal's standard colour scheme applied to every ConPTY session
- **Tab key forwarding** — intercepts Win32 `WM_KEYDOWN` before WPF's HwndHost tab-traversal so Tab reaches the shell

---

## Screenshots

> No screenshots yet.

---

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 |
| Windows | 10 / 11 x64 |
| IDE (optional) | Visual Studio 2022 or VS Code with C# Dev Kit |

---

## Build & Run

```powershell
# Build
dotnet build

# Run
dotnet run --project BetterTerminal.csproj
```

> **Note:** The build copies `OpenConsole.exe` to the output directory. If the app is already running, the copy step will fail with a file-lock error. Close the app before rebuilding.

---

## Run Tests

```powershell
dotnet test
```

Tests live in `BetterTerminal.Tests/` and are excluded from the main project's compile glob. 37 tests covering ViewModels, ShellLocator, and TerminalThemeFactory.

---

## Architecture

BetterTerminal follows the MVVM pattern using `CommunityToolkit.Mvvm`.

```
View  ←→  ViewModel  ←→  Model / Services
```

| Layer | Key files |
|---|---|
| **View** | `MainWindow.xaml/.cs`, `Views/RenameDialog.xaml/.cs`, `App.xaml` |
| **ViewModel** | `ViewModels/MainViewModel.cs`, `ViewModels/SessionViewModel.cs` |
| **Model** | `Models/Session.cs` |
| **Services** | `Services/ShellLocator.cs`, `Services/TerminalThemeFactory.cs` |

**Wiring:**
- `MainWindow` constructs `MainViewModel`, subscribes to `RenameRequested`, and sets `DataContext`
- `MainViewModel.Sessions` is an `ObservableCollection<SessionViewModel>` bound to the sidebar `ListBox`
- `ActiveSession` is two-way bound to `ListBox.SelectedItem`
- Terminal controls (`EasyTerminalControl`) are managed in code-behind, one per session, keyed by `session.Id`
- Inactive terminals use `Visibility.Hidden` (not `Collapsed`) so their HWNDs and shell processes stay alive

### Dependencies

| Package | Version | Purpose |
|---|---|---|
| `CommunityToolkit.Mvvm` | 8.3.2 | `[ObservableProperty]`, `[RelayCommand]`, source generators |
| `EasyWindowsTerminalControl` | 1.0.36 | ConPTY terminal sessions embedded as WPF HwndHost |
| `CI.Microsoft.Windows.Console.ConPTY` | 1.22.250314001 | Native ConPTY runtime (transitive via EasyWindowsTerminalControl) |

---

## Project Structure

```
BetterTerminal/
├── BetterTerminal.sln
├── BetterTerminal.csproj
├── App.xaml / App.xaml.cs          — application entry, global styles/brushes
├── MainWindow.xaml / .cs           — main window: chrome, sidebar, terminal host
├── Models/
│   └── Session.cs                  — plain data record (Id, Title, Shell, WorkingDirectory)
├── ViewModels/
│   ├── MainViewModel.cs            — session list, active session, commands
│   └── SessionViewModel.cs         — per-session state (Title, IsActive)
├── Views/
│   └── RenameDialog.xaml / .cs     — modal rename dialog
├── Services/
│   ├── ShellLocator.cs             — resolves pwsh → powershell.exe → cmd.exe
│   └── TerminalThemeFactory.cs     — builds Campbell dark TerminalTheme
├── Persistence/                    — (M3, not yet implemented)
├── Controls/                       — (future custom controls)
└── BetterTerminal.Tests/
    ├── ViewModels/
    │   ├── MainViewModelTests.cs   — 14 tests
    │   └── SessionViewModelTests.cs — 9 tests
    └── Services/
        ├── ShellLocatorTests.cs        — 4 tests
        └── TerminalThemeFactoryTests.cs — 9 tests
```

---

## Known Issues / Gotchas

### ConPTY native DLL — RID mismatch

`conpty.dll` ships inside the NuGet package under `runtimes/win10-x64/native/`, but .NET 8 only probes `runtimes/win-x64/native/` at runtime (see NETSDK1206). The csproj contains an explicit `<None>/<Link>` item to copy `conpty.dll` to the output root so the Windows DLL loader finds it via the standard exe-directory search.

Similarly, the ConPTY MSBuild targets file is named `Microsoft.Windows.Console.ConPTY.targets` (not matching the package ID), so it is never auto-imported. The csproj imports it explicitly with a `Condition` guard.

### HwndHost Tab forwarding

WPF's `HwndHost` intercepts Tab at the Win32 level before `PreviewKeyDown` fires. The fix is `ComponentDispatcher.ThreadPreprocessMessage` which runs earlier in the message pump. When any terminal HWND has focus, Tab is written directly to the ConPTY (`WriteToTerm("\t")`) and the message is marked handled.

---

## Roadmap

| Milestone | Status | Description |
|---|---|---|
| M1 | Done | Custom chrome, sidebar, session list, new/close/rename, placeholder main area |
| M2 | Done | Real ConPTY sessions via EasyWindowsTerminalControl |
| M3 | Pending | JSON persistence — save and restore sessions across launches |
| M4 | Done | Sidebar drag-to-reorder |
