namespace BuzzFreed.Web.Models
{
    public class QuizResult
    {
        public string UserId { get; set; } = string.Empty;
        public string DiscordGuildId { get; set; } = string.Empty;
        public string QuizId { get; set; } = string.Empty;
        public string QuizTopic { get; set; } = string.Empty;
        public List<string> UserAnswers { get; set; } = new List<string>(); // List of A, B, C, D
        public string ResultPersonality { get; set; } = string.Empty; // e.g., "Espresso"
        public string ResultDescription { get; set; } = string.Empty; // AI-generated description
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
