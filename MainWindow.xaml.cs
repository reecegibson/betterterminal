using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using BetterTerminal.Services;
using BetterTerminal.ViewModels;
using BetterTerminal.Views;
using EasyWindowsTerminalControl;

namespace BetterTerminal;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private readonly MainViewModel _vm;

    // One terminal control per session, keyed by session Id.
    private readonly Dictionary<Guid, EasyTerminalControl> _terminals = new();

    // Idle debounce timers — fire 500 ms after last output to mark session idle.
    private readonly Dictionary<Guid, DispatcherTimer> _idleTimers = new();

    // Per-session buffer for partial OSC title sequences split across ConPTY chunks.
    // ConcurrentDictionary because ConPTY callbacks run on per-session background threads.
    private readonly ConcurrentDictionary<Guid, string> _oscBuffers = new();

    private SessionViewModel? _draggedSession;
    private Point             _dragStartPoint;
    private FrameworkElement? _dragSourceContainer;

    // True while a terminal's hosted Win32 HWND has keyboard focus.
    private bool _anyTerminalHasFocus;

    public MainWindow()
    {
        InitializeComponent();

        // Apply Windows 11 rounded corners via DWM.
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int preference = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
        };

        _vm = new MainViewModel();
        _vm.RenameRequested += OnRenameRequested;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.Sessions.CollectionChanged += OnSessionsChanged;

        // Create terminals for sessions that already exist (from the VM constructor).
        // We do this before setting DataContext so every terminal is ready before
        // any binding fires and tries to show one.
        foreach (var session in _vm.Sessions)
            AttachTerminal(session);

        UpdateVisibility();

        // Intercept Tab at the Win32 message level before WPF's HwndHost tab-traversal runs.
        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        Closed += (_, _) => ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;

        DataContext = _vm;
    }

    // ── Terminal lifecycle ───────────────────────────────────────────────

    private void AttachTerminal(SessionViewModel session)
    {
        var theme = TerminalThemeFactory.CreateDark();

        var term = new EasyTerminalControl
        {
            StartupCommandLine         = session.Shell,
            // Font must be set before Theme so it's picked up by SetTheme internally.
            FontFamilyWhenSettingTheme = new FontFamily("Cascadia Mono"),
            FontSizeWhenSettingTheme   = 12,
            Theme                      = theme,
            HorizontalAlignment        = HorizontalAlignment.Stretch,
            VerticalAlignment          = VerticalAlignment.Stretch,
            // Start hidden; UpdateVisibility will show the right one.
            Visibility                 = Visibility.Hidden,
        };

        // Track whether any terminal HWND has Win32 focus, so we know when to
        // forward Tab in OnThreadPreprocessMessage.
        term.GotKeyboardFocus  += (_, _) => _anyTerminalHasFocus = true;
        term.LostKeyboardFocus += (_, _) =>
            _anyTerminalHasFocus = _terminals.Values.Any(t => t.IsKeyboardFocusWithin);

        _terminals[session.Id] = term;
        TerminalHost.Children.Add(term);

        term.Loaded += (_, _) =>
        {
            if (term.ConPTYTerm is not { } pty) return;
            pty.InterceptOutputToUITerminal = (ref Span<char> output) =>
            {
                if (output.Length == 0) return;

                // Resolve OSC title, handling sequences split across chunks.
                string? oscTitle;
                int trailingPartialStart;

                if (_oscBuffers.TryRemove(session.Id, out var buffered))
                {
                    // Combine buffered partial with current chunk (requires allocation).
                    var combined = string.Concat(buffered, output.ToString());
                    oscTitle = OscTitleParser.ParseOscTitle(combined.AsSpan(), out trailingPartialStart);
                    if (trailingPartialStart >= 0)
                    {
                        var partial = combined[trailingPartialStart..];
                        if (partial.Length <= 512)
                            _oscBuffers[session.Id] = partial;
                    }
                }
                else
                {
                    // Fast path — parse Span directly, no allocation unless partial found.
                    oscTitle = OscTitleParser.ParseOscTitle(output, out trailingPartialStart);
                    if (trailingPartialStart >= 0)
                    {
                        var partial = output[trailingPartialStart..].ToString();
                        if (partial.Length <= 512)
                            _oscBuffers[session.Id] = partial;
                    }
                }

                Dispatcher.BeginInvoke(() =>
                {
                    session.IsActive = true;

                    // Restart idle timer — session goes red 500 ms after output stops.
                    if (!_idleTimers.TryGetValue(session.Id, out var timer))
                    {
                        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                        timer.Tick += (_, _) => { timer.Stop(); session.IsActive = false; };
                        _idleTimers[session.Id] = timer;
                    }
                    timer.Stop();
                    timer.Start();

                    if (oscTitle is not null)
                        session.Title = oscTitle;
                });
            };
        };
    }

    private void DetachTerminal(SessionViewModel session)
    {
        if (!_terminals.TryGetValue(session.Id, out var term)) return;
        TerminalHost.Children.Remove(term);
        _terminals.Remove(session.Id);
        // EasyTerminalControl doesn't implement IDisposable publicly,
        // but try anyway in case a future version does.
        (term as IDisposable)?.Dispose();

        if (_idleTimers.TryGetValue(session.Id, out var timer))
        {
            timer.Stop();
            _idleTimers.Remove(session.Id);
        }

        _oscBuffers.TryRemove(session.Id, out _);
    }

    /// <summary>
    /// Shows the terminal for the active session; hides all others.
    /// Uses Hidden (not Collapsed) so each HwndHost keeps its HWND alive
    /// and the shell process keeps running in the background.
    /// </summary>
    private void UpdateVisibility()
    {
        var activeId = _vm.ActiveSession?.Id;
        foreach (var (id, term) in _terminals)
            term.Visibility = id == activeId ? Visibility.Visible : Visibility.Hidden;
    }

    // ── Collection / property change handlers ───────────────────────────

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (SessionViewModel s in e.NewItems)
                AttachTerminal(s);

        if (e.OldItems is not null)
            foreach (SessionViewModel s in e.OldItems)
                DetachTerminal(s);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveSession))
            UpdateVisibility();
    }

    // ── Keyboard forwarding ──────────────────────────────────────────────

    /// <summary>
    /// Intercepts Win32 messages before WPF processes them.
    /// WPF's HwndHost tab-traversal runs at the Win32 level and never reaches
    /// OnPreviewKeyDown, so this is the only reliable interception point.
    /// </summary>
    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (handled || !IsActive) return;
        if (_vm.ActiveSession is not { } session) return;
        if (!_terminals.TryGetValue(session.Id, out var term)) return;

        const int WM_KEYDOWN = 0x0100;
        const int VK_TAB     = 0x09;
        const int VK_C       = 0x43;
        const int VK_V       = 0x56;
        const int VK_SHIFT   = 0x10;
        const int VK_CONTROL = 0x11;

        if (msg.message != WM_KEYDOWN) return;

        int  vk        = (int)msg.wParam;
        bool ctrlDown  = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
        bool shiftDown = (GetKeyState(VK_SHIFT)   & 0x8000) != 0;

        if (vk == VK_TAB && !ctrlDown && _anyTerminalHasFocus)
        {
            term.ConPTYTerm?.WriteToTerm("\t");
            handled = true;
        }
        else if (ctrlDown && vk == VK_C && Keyboard.FocusedElement is not TextBox)
        {
            var selected = term.Terminal?.GetSelectedText();
            if (!string.IsNullOrEmpty(selected))
            {
                // Selection exists → copy to clipboard.
                Clipboard.SetText(selected);
                handled = true;
            }
            else if (!shiftDown)
            {
                // No selection, no Shift → send interrupt (ETX).
                term.ConPTYTerm?.WriteToTerm("\x03");
                handled = true;
            }
            // Ctrl+Shift+C with no selection → no-op.
        }
        else if (ctrlDown && vk == VK_V && Keyboard.FocusedElement is not TextBox)
        {
            // Ctrl+V → paste clipboard text into the terminal.
            // Deliberately does not require _anyTerminalHasFocus: WPF's GotKeyboardFocus
            // on an HwndHost child can be unreliable, so we guard instead by ensuring no
            // WPF text input (rename dialog, etc.) currently has focus.
            var text = Clipboard.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                term.ConPTYTerm?.WriteToTerm(text);
                handled = true;
            }
        }
    }

    // ── Window controls ──────────────────────────────────────────────────

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    // ── Clear history ───────────────────────────────────────────────────

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.ActiveSession is not { } session) return;
        if (!_terminals.TryGetValue(session.Id, out var term)) return;
        if (term.ConPTYTerm is null) return;

        // Send the CSI 3 J escape sequence directly to the terminal renderer
        // via WriteToUITerminal.  This bypasses the shell entirely — nothing
        // is typed into the prompt — and tells the renderer to purge its
        // scrollback buffer while leaving visible content untouched.
        var seq = TerminalHistoryService.BuildClearSequence();
        term.ConPTYTerm.WriteToUITerminal(seq.AsSpan());
    }

    // ── Drag-and-drop reordering ─────────────────────────────────────────

    private void SessionList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _draggedSession = (e.OriginalSource as FrameworkElement)?.DataContext as SessionViewModel;
    }

    private void SessionList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedSession is null) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragSourceContainer = SessionList.ItemContainerGenerator
                                   .ContainerFromItem(_draggedSession) as FrameworkElement;
        if (_dragSourceContainer is not null)
            _dragSourceContainer.Opacity = 0.4;

        DragDrop.DoDragDrop((DependencyObject)sender, _draggedSession, DragDropEffects.Move);

        // DoDragDrop blocks until drag ends — restore unconditionally here.
        if (_dragSourceContainer is not null)
        {
            _dragSourceContainer.Opacity = 1.0;
            _dragSourceContainer = null;
        }
        _draggedSession = null;
    }

    private void SessionList_DragOver(object sender, DragEventArgs e)
    {
        UpdateDropLine(e.GetPosition(SessionList));
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void SessionList_Drop(object sender, DragEventArgs e)
    {
        HideDropLine();
        if (_draggedSession is null) return;

        var listBox = (ListBox)sender;
        int targetIndex = GetDropIndex(listBox, e.GetPosition(listBox));
        int sourceIndex = _vm.Sessions.IndexOf(_draggedSession);

        if (sourceIndex >= 0 && sourceIndex != targetIndex)
            _vm.Sessions.Move(sourceIndex, targetIndex);

        _draggedSession = null;
    }

    private void SessionList_DragLeave(object sender, DragEventArgs e)
        => HideDropLine();

    private void UpdateDropLine(Point dropPos)
    {
        double lineY = SessionList.ActualHeight - 2; // default: after last item

        for (int i = 0; i < SessionList.Items.Count; i++)
        {
            if (SessionList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement item)
                continue;
            var topY = item.TranslatePoint(new Point(0, 0), SessionList).Y;
            var midY = topY + item.ActualHeight / 2;
            if (dropPos.Y < midY) { lineY = topY; break; }
            if (i == SessionList.Items.Count - 1)
                lineY = topY + item.ActualHeight;
        }

        Canvas.SetTop(DropLine, lineY - 1);
        Canvas.SetLeft(DropLine, 8);
        DropLine.Width      = DropCanvas.ActualWidth - 16;
        DropLine.Visibility = Visibility.Visible;
    }

    private void HideDropLine()
        => DropLine.Visibility = Visibility.Collapsed;

    private static int GetDropIndex(ListBox listBox, Point dropPos)
    {
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement item)
                continue;
            var midY = item.TranslatePoint(new Point(0, item.ActualHeight / 2), listBox).Y;
            if (dropPos.Y < midY) return i;
        }
        return listBox.Items.Count - 1;
    }

    // ── Rename handler ───────────────────────────────────────────────────

    private void OnRenameRequested(SessionViewModel session)
    {
        var dialog = new RenameDialog(session.Title) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.NewName is not null)
            session.Title = dialog.NewName;
    }
}
