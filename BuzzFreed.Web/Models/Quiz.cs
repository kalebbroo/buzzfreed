namespace BuzzFreed.Web.Models
{
    /// <summary>
    /// Represents a complete quiz with questions
    /// Supports both personality quizzes and competitive multiplayer quizzes
    /// </summary>
    public class Quiz
    {
        /// <summary>
        /// Unique identifier for this quiz
        /// Alias: Use QuizId property for new code
        /// </summary>
        public string Id
        {
            get => QuizId;
            set => QuizId = value;
        }

        /// <summary>
        /// Unique identifier for this quiz (preferred property name)
        /// </summary>
        public string QuizId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Quiz title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Quiz topic or theme
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// List of questions in this quiz
        /// </summary>
        public List<Question> Questions { get; set; } = new List<Question>();

        /// <summary>
        /// For personality quizzes: maps answer letter (A, B, C, D) to personality type
        /// e.g., {"A": "Espresso", "B": "Latte", "C": "Cappuccino", "D": "Mocha"}
        /// </summary>
        public Dictionary<string, string> ResultPersonalities { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Optional description of the quiz
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// When this quiz was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Creator user ID or "AI" for generated quizzes
        /// </summary>
        public string? CreatedBy { get; set; }
    }
}
