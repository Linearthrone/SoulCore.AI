using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Controls;

/// <summary>
/// One/Many choice. The radial and Settings → System each host one of these and apply the same mode.
/// </summary>
public sealed class PersonaModeControl : StackPanel
{
    private readonly PersonaModeControlState _state = new();
    private readonly Button _one;
    private readonly Button _many;

    public PersonaModeControl()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        _one = Make("One", PersonaChatMode.One);
        _many = Make("Many", PersonaChatMode.Many);
        Children.Add(_one);
        Children.Add(_many);
        Apply(PersonaChatMode.One);
    }

    public string ShownMode => _state.ShownMode;

    public string ShownLabel => _state.ShownLabel;

    public event EventHandler<string>? ModeChosen;

    public void Apply(string? mode)
    {
        _state.Apply(mode);
        Mark(_one, _state.OneSelected);
        Mark(_many, _state.ManySelected);
    }

    private Button Make(string label, string mode)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 72,
            MinHeight = 32
        };
        button.Classes.Add("frame-nav");
        button.Click += (_, _) => ModeChosen?.Invoke(this, mode);
        return button;
    }

    private static void Mark(Button button, bool selected)
    {
        button.FontWeight = selected ? FontWeight.Bold : FontWeight.Normal;
        if (selected)
            button.Classes.Add("primary");
        else
            button.Classes.Remove("primary");
    }
}
