using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace House.ChatDesktop.Models;

public sealed class ChatMessage : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private IImage? _image;
    private string? _mediaPath;
    private string? _mediaId;

    /// <summary>Stable id for SQLite upsert / dedupe (LLMOD-style).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public required string Role { get; init; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasText));
        }
    }

    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;

    /// <summary>Correlation id from SoulCore frame (for streaming assistant bubbles).</summary>
    public string? FrameId { get; set; }

    /// <summary>Companion media id from chat.done when Host ships MMS.</summary>
    public string? MediaId
    {
        get => _mediaId;
        set
        {
            if (_mediaId == value) return;
            _mediaId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Local path for attached / cached MMS image.</summary>
    public string? MediaPath
    {
        get => _mediaPath;
        set
        {
            if (_mediaPath == value) return;
            _mediaPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public IImage? Image
    {
        get => _image;
        set
        {
            if (ReferenceEquals(_image, value)) return;
            _image = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => Image is not null || !string.IsNullOrWhiteSpace(MediaPath);

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public string DisplayRole => Role switch
    {
        "user" => "You",
        "assistant" => "Victoria",
        "system" => "System",
        _ => Role
    };

    public string DisplayTime => At.ToLocalTime().ToString("h:mm tt");

    /// <summary>Drives the user-bubble style selector in the transcript template.</summary>
    public bool IsUser => Role == "user";

    public bool IsSystem => Role == "system";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
