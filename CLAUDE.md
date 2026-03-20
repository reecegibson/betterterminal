# CLAUDE.md — BetterTerminal contributor guide

Authoritative reference for building, testing, and modifying BetterTerminal.

---

## Build & Run

```powershell
# Build
dotnet build

# Run
dotnet run --project BetterTerminal.csproj

# Build + run in one step
dotnet run
```

> **Important:** The build copies `OpenConsole.exe` and `conpty.dll` to the output directory. If the app is already running those files are locked — the build will fail. Always close the app before rebuilding.

---

## Tests

```powershell
dotnet test
```

Test project: `BetterTerminal.Tests/` (separate `.csproj`, excluded from the main project's SDK glob via `<Compile Remove="BetterTerminal.Tests\**\*"/>`).

54 tests total — all must pass before merging.

---

## Project Structure

```
BetterTerminal/
├── BetterTerminal.sln
├── BetterTerminal.csproj           — net8.0-windows; UseWPF; ConPTY workarounds (see below)
├── App.xaml                        — global brushes, styles (GhostBtn, AccentBtn, TitleBarBtn,
│                                     CloseTitleBarBtn, ContextMenu/MenuItem/Separator templates)
├── App.xaml.cs                     — OnStartup creates MainWindow; no StartupUri
├── MainWindow.xaml                 — layout: 2-row × 3-col Grid (title bar / content)
├── MainWindow.xaml.cs              — terminal lifecycle, drag-drop, Tab forwarding
├── Models/
│   └── Session.cs                  — plain record: Id (Guid), Title, Shell, WorkingDirectory
├── ViewModels/
│   ├── MainViewModel.cs            — Sessions (ObservableCollection), ActiveSession,
│   │                                 AddSession / CloseSession / RequestRename commands
│   └── SessionViewModel.cs         — Title ([ObservableProperty]), IsActive, ToModel()
├── Views/
│   └── RenameDialog.xaml/.cs       — SizeToContent=Height modal; IsDefault/IsCancel buttons
├── Services/
│   ├── OscTitleParser.cs           — ParseOscTitle(): extracts OSC 0/2 titles, handles fragmentation
│   ├── ShellLocator.cs             — Resolve(): pwsh → powershell.exe → cmd.exe
│   └── TerminalThemeFactory.cs     — CreateDark(): Campbell colour scheme for EasyTerminalControl
├── Persistence/                    — empty (M3 not yet implemented)
├── Controls/                       — empty (future)
└── BetterTerminal.Tests/
    ├── ViewModels/MainViewModelTests.cs      — 14 tests
    ├── ViewModels/SessionViewModelTests.cs   — 9 tests
    ├── Services/OscTitleParserTests.cs        — 17 tests
    ├── Services/ShellLocatorTests.cs         — 4 tests
    └── Services/TerminalThemeFactoryTests.cs — 9 tests
```

---

## Architecture

### Layers

```
View  ←→  ViewModel  ←→  Model / Services
```

- **Model** (`Models/Session.cs`) — pure data, no WPF dependencies
- **ViewModel** (`ViewModels/`) — `ObservableObject` + `[ObservableProperty]` / `[RelayCommand]`; no UI code
- **View** (`MainWindow`, `RenameDialog`) — XAML + code-behind for UI-only logic (terminal lifecycle, drag-drop, dialogs)
- **Services** — `ShellLocator` (static), `TerminalThemeFactory` (static)

### DataContext wiring

`MainWindow` constructor:
1. `InitializeComponent()`
2. Apply DWM rounded corners via `DwmSetWindowAttribute` (attr 33 = `DWMWCP_ROUND`, value 2)
3. `new MainViewModel()` → subscribe to `RenameRequested` + `PropertyChanged` + `Sessions.CollectionChanged`
4. Pre-create terminals for any sessions already in the VM
5. `DataContext = _vm` (set last so bindings fire after terminals are ready)

### Terminal lifecycle

- One `EasyTerminalControl` per `SessionViewModel`, stored in `Dictionary<Guid, EasyTerminalControl> _terminals`
- Added/removed in `OnSessionsChanged` (CollectionChanged handler)
- Inactive terminals use `Visibility.Hidden` (not `Collapsed`) so their HWNDs stay alive and shells keep running
- `pty.InterceptOutputToUITerminal` callback: called on terminal thread → synchronously calls `ParseOscTitle` (Span, can't be captured), then `Dispatcher.BeginInvoke` to update `session.IsActive` + restart idle timer + optionally update title

---

## Key Patterns

### CommunityToolkit.Mvvm

```csharp
// ViewModel property — generates backing field + INotifyPropertyChanged
[ObservableProperty]
private string _title = "";

// Command — generates IRelayCommand property
[RelayCommand]
private void AddSession() { ... }
```

Partial class required. `AllowUnsafeBlocks=true` in csproj for source generators.

### ContextMenu binding trick

The sidebar ContextMenu is defined on a `Border` inside a `DataTemplate`. To reach `MainViewModel` commands:

```xml
<!-- Border carries a reference to the window's DataContext (MainViewModel) -->
<Border Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Window}}">
    <Border.ContextMenu>
        <ContextMenu>
            <!-- Commands come from Tag (MainViewModel) -->
            <MenuItem Command="{Binding PlacementTarget.Tag.CloseSessionCommand,
                                        RelativeSource={RelativeSource AncestorType=ContextMenu}}"
                      CommandParameter="{Binding PlacementTarget.DataContext,
                                                  RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
        </ContextMenu>
    </Border.ContextMenu>
</Border>
```

### Tab key forwarding

WPF's HwndHost intercepts Tab at Win32 level before `PreviewKeyDown`. Fix:

```csharp
ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;

void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
{
    if (handled || !_anyTerminalHasFocus || !IsActive) return;
    if (msg.message == WM_KEYDOWN && (int)msg.wParam == VK_TAB)
    {
        term.ConPTYTerm?.WriteToTerm("\t");
        handled = true;
    }
}
```

### OSC title parsing

`ParseOscTitle(ReadOnlySpan<char> output)` — scans raw terminal output for `ESC ] 0 ;` or `ESC ] 2 ;` sequences, terminated by BEL (`\x07`) or ST (`ESC \`). Must be called synchronously before the Span is captured in a delegate.

### ConPTY session font

Font **must** be set before `Theme` on `EasyTerminalControl`:

```csharp
var term = new EasyTerminalControl
{
    FontFamilyWhenSettingTheme = new FontFamily("Cascadia Mono"),
    FontSizeWhenSettingTheme   = 12,
    Theme                      = TerminalThemeFactory.CreateDark(),
    ...
};
```

---

## Colour Palette

Defined as `SolidColorBrush` resources in `App.xaml`:

| Resource key | Hex | Usage |
|---|---|---|
| `BgBrush` | `#1E1E1E` | Main content area background |
| `SidebarBrush` | `#252526` | Sidebar + title bar left panel |
| `AccentBrush` | `#007ACC` | Active session indicator, selection highlight |
| `TextBrush` | `#CCCCCC` | Primary text |
| `DimBrush` | `#858585` | Secondary / inactive text |
| `HoverBrush` | `#2A2D2E` | Session row hover state |
| `ActiveBrush` | `#37373D` | Selected session row background |
| `SepBrush` | `#3C3C3C` | Sidebar / content divider (1 px) |
| `InputBgBrush` | `#2D2D30` | Dialog input fields |

Title bar close button hover: `#C42B1C` (Win11 red). Min/Max hover: `#3E3E42`.

---

## Known Gotchas

### WPF XAML compiler implicit usings

The XAML compiler generates a temp project that doesn't inherit `ImplicitUsings`. Always add explicit `using` statements at the top of `.xaml.cs` files:

```csharp
using System.IO;
using System.Windows.Controls;
// etc.
```

### SDK glob picks up subdirectories

Without the explicit exclusion, the main csproj would compile test files:

```xml
<Compile Remove="BetterTerminal.Tests\**\*" />
```

### `Background=null` is not hit-testable

A `Border` or `Panel` with `Background=null` does not receive mouse events. Always use `Background="Transparent"` on clickable rows/cells.

### WPF Button ignores inline Background/Foreground

`Button` renders through its `ControlTemplate` which uses `SystemColors`. Setting `Background`/`Foreground` inline is ignored. Use a custom `ControlTemplate` (see `GhostBtn`, `AccentBtn`, `TitleBarBtn`, `CloseTitleBarBtn` in `App.xaml`).

### ContextMenu Background setter is ignored

The OS wraps `ContextMenu` in its own chrome, so `Background` property setters have no effect. The full `ControlTemplate` must be replaced (see `App.xaml` ContextMenu style).

### RenameDialog height

Use `SizeToContent="Height"` on the dialog window. A fixed pixel height leaves no content area after the `ToolWindow` chrome is applied.

### ConPTY — MSBuild targets not auto-imported

`CI.Microsoft.Windows.Console.ConPTY` ships its targets as `Microsoft.Windows.Console.ConPTY.targets` (name doesn't match the package ID), so NuGet never auto-imports it. The csproj imports it explicitly:

```xml
<ConptyRequiresx64Host>true</ConptyRequiresx64Host>

<Import Project="$(NuGetPackageRoot)ci.microsoft.windows.console.conpty\...\build\Microsoft.Windows.Console.ConPTY.targets"
        Condition="Exists(...)" />
```

### ConPTY — `conpty.dll` RID mismatch

`conpty.dll` is packaged as `runtimes/win10-x64/native/` but .NET 8 only probes `runtimes/win-x64/native/` (NETSDK1206). Fix: `<None>/<Link>` item in csproj copies the DLL to the output root:

```xml
<None Include="$(NuGetPackageRoot)ci.microsoft.windows.console.conpty\...\runtimes\win10-x64\native\conpty.dll">
  <Link>conpty.dll</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

### DWM rounded corners

Must be applied after the HWND is created (`SourceInitialized` event), not in the constructor:

```csharp
SourceInitialized += (_, _) =>
{
    var hwnd = new WindowInteropHelper(this).Handle;
    int preference = 2; // DWMWCP_ROUND
    DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
};
```

---

## Milestones

| # | Status | Description |
|---|---|---|
| M1 | Done | Custom chrome title bar, sidebar, session list, new/close/rename, placeholder main area |
| M2 | Done | Real ConPTY sessions via EasyWindowsTerminalControl |
| M3 | **Pending** | JSON persistence — save and restore sessions across launches |
| M4 | Done | Sidebar drag-to-reorder (native WPF DragDrop + `ObservableCollection.Move`) |

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `CommunityToolkit.Mvvm` | 8.3.2 | MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| `EasyWindowsTerminalControl` | 1.0.36 | ConPTY WPF control (`EasyTerminalControl`, `HwndHost`) |
| `CI.Microsoft.Windows.Console.ConPTY` | 1.22.250314001 | Native ConPTY runtime (transitive; requires explicit MSBuild import + DLL copy) |
| `xunit` | 2.6.4 | Test framework (test project only) |
| `Microsoft.NET.Test.Sdk` | — | Test runner (test project only) |
