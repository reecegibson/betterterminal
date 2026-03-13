using System.Globalization;
using BetterTerminal.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BetterTerminal.ViewModels;

/// <summary>
/// UI state for one session entry in the sidebar.
/// Title is observable so renaming updates the list in real-time.
/// </summary>
public partial class SessionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isActive;

    public Guid   Id    { get; }
    public string Shell { get; }

    public SessionViewModel(Session model)
    {
        Id     = model.Id;
        Shell  = model.Shell;
        _title = model.Title;
    }

    /// <summary>
    /// Leading emoji / Nerd-Font glyph extracted from <see cref="Title"/>, or null.
    /// OSC-emitted titles like "🐍 python" or " src" are split here.
    /// </summary>
    public string? Icon         => ExtractIcon(Title);

    /// <summary>Title with any leading icon (and its trailing space) stripped.</summary>
    public string  DisplayTitle => StripIcon(Title);

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(DisplayTitle));
    }

    // ── Icon parsing helpers ─────────────────────────────────────────────

    private static string? ExtractIcon(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        var grapheme = StringInfo.GetNextTextElement(title);
        return IsIconGrapheme(grapheme) ? grapheme : null;
    }

    private static string StripIcon(string title)
    {
        var icon = ExtractIcon(title);
        return icon is null ? title : title[icon.Length..].TrimStart();
    }

    /// <summary>
    /// Returns true for grapheme clusters that are visual icons rather than text:
    ///   • Multi-code-unit clusters  — surrogate pairs (emoji above U+FFFF), ZWJ sequences
    ///   • U+2600–U+FFFF             — Misc symbols, dingbats, emoji blocks
    ///   • U+E000–U+F8FF             — Nerd Fonts private-use area
    /// </summary>
    private static bool IsIconGrapheme(string grapheme)
    {
        if (string.IsNullOrEmpty(grapheme)) return false;
        if (grapheme.Length > 1) return true;          // surrogate pair or ZWJ sequence
        var cp = grapheme[0];
        return cp >= '\u2600'                           // symbols / emoji / dingbats
            || (cp >= '\uE000' && cp <= '\uF8FF');      // Nerd Fonts PUA
    }

    /// <summary>Snapshot back to a persisted model.</summary>
    public Session ToModel() => new() { Id = Id, Shell = Shell, Title = Title };
}
