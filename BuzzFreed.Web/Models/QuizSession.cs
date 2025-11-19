namespace BuzzFreed.Web.Models
{
    /// <summary>
    /// Tracks an active quiz session in memory
    /// </summary>
    public class QuizSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public Quiz Quiz { get; set; } = new Quiz();
        public List<string> UserAnswers { get; set; } = new List<string>();
        public int CurrentQuestionIndex { get; set; } = 0;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public bool IsCompleted { get; set; } = false;
    }
}
