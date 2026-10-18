using Avalonia.Controls;
using Avalonia.Interactivity;

namespace House.ChatDesktop;

/// <summary>Simple title prompt for operator-added victoria_tasks rows.</summary>
public partial class TaskAddWindow : Window
{
    public string? TitleText { get; private set; }

    public TaskAddWindow()
    {
        InitializeComponent();
        Opened += (_, _) => TitleBox?.Focus();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        var title = TitleBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            if (ErrorText is not null)
            {
                ErrorText.IsVisible = true;
                ErrorText.Text = "Title is required.";
            }
            return;
        }

        TitleText = title;
        Close(true);
    }
}
