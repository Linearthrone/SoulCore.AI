using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace House.ChatDesktop.Models;

public sealed class ChatMessage : INotifyPropertyChanged
{
    private string _text = string.Empty;

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
    public string? FrameId { get; init; }

    public string DisplayRole => Role switch
    {
        "user" => "You",
        "assistant" => "Victoria",
        "system" => "System",
        _ => Role
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
