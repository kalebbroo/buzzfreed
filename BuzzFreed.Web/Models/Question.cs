namespace BuzzFreed.Web.Models
{
    /// <summary>
    /// Represents a quiz question
    /// Supports both personality quizzes and competitive multiplayer quizzes
    /// </summary>
    public class Question
    {
        /// <summary>
        /// Unique identifier for this question
        /// </summary>
        public string QuestionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The question text
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// List of answer options
        /// Alias: Use Answers property for new code
        /// </summary>
        public List<string> Options
        {
            get => Answers;
            set => Answers = value;
        }

        /// <summary>
        /// List of answer options (preferred property name)
        /// </summary>
        public List<string> Answers { get; set; } = new List<string>();

        /// <summary>
        /// For personality quizzes: personality mapping (A, B, C, or D)
        /// </summary>
        public string CorrectAnswer { get; set; } = string.Empty;

        /// <summary>
        /// For competitive quizzes: index of the correct answer (0-based)
        /// </summary>
        public int CorrectAnswerIndex { get; set; } = 0;

        /// <summary>
        /// Optional image URL for visual questions
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Optional explanation shown after answering
        /// </summary>
        public string? Explanation { get; set; }

        /// <summary>
        /// Difficulty level (easy, medium, hard)
        /// </summary>
        public string? Difficulty { get; set; }

        /// <summary>
        /// Category or topic tag
        /// </summary>
        public string? Category { get; set; }
    }
}
