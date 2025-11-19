namespace BuzzFreed.Web.AI.Models
{
    /// <summary>
    /// Represents a message in a chat conversation
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty; // system, user, assistant
        public string Content { get; set; } = string.Empty;
        public string? Name { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public static ChatMessage System(string content) => new ChatMessage { Role = "system", Content = content };
        public static ChatMessage User(string content) => new ChatMessage { Role = "user", Content = content };
        public static ChatMessage Assistant(string content) => new ChatMessage { Role = "assistant", Content = content };
    }
}
