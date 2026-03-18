using System.Collections.Generic;

namespace FelderBot.Models;

public sealed class ChatMessage
{
    public bool IsUser { get; init; }
    public string Content { get; set; } = "";
    public bool IsStreaming { get; set; }
    public string? Error { get; set; }
    public List<string>? Sources { get; set; }
}
