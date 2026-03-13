using System.Windows;

namespace BetterTerminal.Views;

public partial class RenameDialog : Window
{
    /// <summary>Set when the user clicks Rename; null if cancelled.</summary>
    public string? NewName { get; private set; }

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        NameBox.SelectAll();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        NewName = name;
        DialogResult = true;
    }
}
