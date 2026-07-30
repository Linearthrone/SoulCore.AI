using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace House.ChatDesktop.Models;

public sealed class ChatMessage : INotifyPropertyChanged
{
    private string _text = string.Empty;

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
        }
    }

    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;

    /// <summary>Correlation id from SoulCore frame (for streaming assistant bubbles).</summary>
    public string? FrameId { get; set; }

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
