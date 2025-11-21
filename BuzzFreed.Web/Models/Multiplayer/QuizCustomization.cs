namespace BuzzFreed.Web.Models.Multiplayer;

/// <summary>
/// Quiz customization settings chosen by room host
/// Controls how AI generates the quiz content
/// </summary>
public class QuizCustomization
{
    /// <summary>
    /// Main quiz topic or theme
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Category selection (Food, Movies, Gaming, Science, History, etc.)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Custom prompt from host for specific quiz ideas
    /// </summary>
    public string? CustomPrompt { get; set; }

    /// <summary>
    /// Question style (Classic, Deep, Chaotic, Rapid, Story)
    /// </summary>
    public QuestionStyle Style { get; set; } = QuestionStyle.Classic;

    /// <summary>
    /// Difficulty level (Casual, Challenging, Absurd)
    /// </summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Casual;

    /// <summary>
    /// Number of questions in the quiz
    /// </summary>
    public int QuestionCount { get; set; } = 10;

    /// <summary>
    /// Include AI-generated images for questions
    /// </summary>
    public bool IncludeImages { get; set; } = false;

    /// <summary>
    /// Style for generated images (Realistic, Cartoon, Anime, etc.)
    /// </summary>
    public string? ImageStyle { get; set; }

    /// <summary>
    /// Mood for generated images (Cheerful, Dark, Energetic, Calm)
    /// </summary>
    public string? ImageMood { get; set; }

    /// <summary>
    /// Number of answer options per question (typically 4)
    /// </summary>
    public int AnswerCount { get; set; } = 4;

    /// <summary>
    /// Include explanations for correct answers
    /// </summary>
    public bool IncludeExplanations { get; set; } = true;

    /// <summary>
    /// Mix of pop culture, niche, and common knowledge
    /// </summary>
    public string? MixStyle { get; set; }
}

/// <summary>
/// Question style options
/// </summary>
public enum QuestionStyle
{
    /// <summary>
    /// Traditional trivia questions
    /// </summary>
    Classic,

    /// <summary>
    /// Thought-provoking, deeper questions
    /// </summary>
    Deep,

    /// <summary>
    /// Random, unpredictable, wild questions
    /// </summary>
    Chaotic,

    /// <summary>
    /// Quick-fire, fast-paced questions
    /// </summary>
    Rapid,

    /// <summary>
    /// Narrative-driven questions with storylines
    /// </summary>
    Story
}

/// <summary>
/// Difficulty levels
/// </summary>
public enum Difficulty
{
    /// <summary>
    /// Easy, accessible questions
    /// </summary>
    Casual,

    /// <summary>
    /// Medium difficulty, requires knowledge
    /// </summary>
    Challenging,

    /// <summary>
    /// Hard, niche, or absurd questions
    /// </summary>
    Absurd
}
