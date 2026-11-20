using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace House.ChatDesktop.Controls;

/// <summary>Glass-themed yes/no dialog (PROP-22.9 port warning, PROP-22.3 mailbox confirm).</summary>
public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog modally over <paramref name="owner"/>; true only when confirmed.</summary>
    public static async Task<bool> ShowAsync(
        Window owner,
        string title,
        string message,
        string confirmText = "Confirm",
        string? warning = null)
    {
        var dialog = new ConfirmDialogWindow();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;
        if (!string.IsNullOrWhiteSpace(warning))
        {
            dialog.WarningText.Text = warning;
            dialog.WarningText.IsVisible = true;
        }

        return await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
