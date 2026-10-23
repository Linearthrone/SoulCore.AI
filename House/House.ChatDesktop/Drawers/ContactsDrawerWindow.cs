using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Drawers;

public sealed class ContactsDrawerWindow : Window
{
    private readonly ListBox _list = new();
    private readonly TextBlock _name = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _description = new()
    {
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap
    };
    private readonly ScrollViewer _descriptionScroll;
    private readonly TextBlock _id = new() { FontSize = 12 };
    private readonly TextBlock _quarantine = new()
    {
        Text = "Quarantine: this persona's memories and local transcript stay on this id. They do not follow a switch.",
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12
    };
    private readonly TextBlock _initials = new()
    {
        FontSize = 28,
        FontWeight = FontWeight.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Image _photo = new() { Width = 160, Height = 160, Stretch = Stretch.UniformToFill };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button _open = new() { Content = "Open chat", MinWidth = 110 };
    private readonly Button _edit = new() { Content = "Edit", MinWidth = 88 };
    private readonly Button _pin = new() { Content = "Pin favorite", MinWidth = 120 };
    private readonly Button _photoButton = new() { Content = "Set photo", MinWidth = 100 };
    private IReadOnlyList<FavoriteSlot> _favorites = Array.Empty<FavoriteSlot>();
    private Bitmap? _bitmap;

    public ContactsDrawerWindow()
    {
        _list.MinWidth = 180;
        _list.MaxWidth = 220;
        _open.Classes.Add("primary");
        _edit.Classes.Add("ghost");
        _pin.Classes.Add("ghost");
        _photoButton.Classes.Add("ghost");
        _open.IsEnabled = false;
        _edit.IsEnabled = false;
        _pin.IsEnabled = false;
        _photoButton.IsEnabled = false;
        _descriptionScroll = new ScrollViewer
        {
            MaxHeight = 120,
            IsVisible = false,
            Content = _description
        };

        var portrait = new Panel
        {
            Width = 168,
            Height = 168,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var well = new Border
        {
            Width = 168,
            Height = 168,
            CornerRadius = new CornerRadius(84),
            ClipToBounds = true,
            Child = new Panel
            {
                Children = { _photo, _initials }
            }
        };
        well.Classes.Add("carbon");
        portrait.Children.Add(well);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _open, _edit, _pin, _photoButton }
        };
        var detail = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16, 0, 0, 0),
            Children = { portrait, _name, _descriptionScroll, _id, _quarantine, buttons, _status }
        };
        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Margin = new Thickness(12)
        };
        body.Children.Add(_list);
        body.Children.Add(detail);
        Grid.SetColumn(detail, 1);

        GlassDrawerChrome.Attach(this, "Agent contacts", body, 720, 560);

        _list.SelectionChanged += (_, _) => ShowSelected();
        _open.Click += (_, _) =>
        {
            if (Selected is { } persona)
                OpenChatRequested?.Invoke(this, persona);
        };
        _edit.Click += (_, _) =>
        {
            if (Selected is { } persona)
                EditRequested?.Invoke(this, persona);
        };
        _pin.Click += (_, _) =>
        {
            if (Selected is { } persona)
                PinRequested?.Invoke(this, persona);
        };
        _photoButton.Click += (_, _) =>
        {
            if (Selected is { } persona)
                PhotoRequested?.Invoke(this, persona);
        };
    }

    public event EventHandler<PersonaPackInfo>? OpenChatRequested;
    public event EventHandler<PersonaPackInfo>? EditRequested;
    public event EventHandler<PersonaPackInfo>? PinRequested;
    public event EventHandler<PersonaPackInfo>? PhotoRequested;

    public PersonaPackInfo? Selected => _list.SelectedItem as PersonaPackInfo;

    public void Bind(IReadOnlyList<PersonaPackInfo> personas, IReadOnlyList<FavoriteSlot> favorites)
    {
        _favorites = favorites;
        var keep = Selected?.PersonaId;
        _list.ItemsSource = personas.ToList();
        if (!string.IsNullOrWhiteSpace(keep))
        {
            _list.SelectedItem = personas.FirstOrDefault(p =>
                string.Equals(p.PersonaId, keep, StringComparison.OrdinalIgnoreCase));
        }

        ShowSelected();
    }

    public void SetStatus(string? text) => _status.Text = text ?? "";

    private void ShowSelected()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _photo.Source = null;
        var persona = Selected;
        var enabled = persona is not null;
        _open.IsEnabled = enabled;
        _edit.IsEnabled = enabled;
        _pin.IsEnabled = enabled;
        _photoButton.IsEnabled = enabled;
        if (persona is null)
        {
            _name.Text = "No persona selected";
            _description.Text = "";
            _descriptionScroll.IsVisible = false;
            _id.Text = "";
            _initials.Text = "";
            _initials.IsVisible = true;
            return;
        }

        _name.Text = persona.DisplayName;
        _description.Text = PersonaWizardLogic.ContactDescriptionText(persona.PhysicalDescription);
        _descriptionScroll.IsVisible = _description.Text.Length > 0;
        _id.Text = persona.PersonaId;
        var favorite = _favorites.FirstOrDefault(f =>
            string.Equals(f.PersonaId, persona.PersonaId, StringComparison.OrdinalIgnoreCase));
        if (favorite?.PhotoPath is { Length: > 0 } path && File.Exists(path))
        {
            try
            {
                _bitmap = new Bitmap(path);
                _photo.Source = _bitmap;
                _initials.IsVisible = false;
                return;
            }
            catch
            {
                _bitmap = null;
                _photo.Source = null;
            }
        }

        _initials.Text = FavoriteRoster.InitialsFor(persona.DisplayName);
        _initials.IsVisible = true;
    }
}
