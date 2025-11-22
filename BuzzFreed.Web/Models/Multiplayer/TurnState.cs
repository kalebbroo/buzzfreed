namespace BuzzFreed.Web.Models.Multiplayer;

/// <summary>
/// Represents a single turn/round in the game
///
/// TURN FLOW:
/// 1. Question Phase - Display question, players can answer
/// 2. Answering Phase - Player submits answer, timer running
/// 3. Reaction Phase - Brief pause for reactions (3-5s)
/// 4. Results Phase - Show if answer was correct, update scores
/// 5. Transition - Move to next player/question
///
/// DESIGN NOTES:
/// - Each turn has its own timer
/// - Phase transitions are automatic unless paused
/// - Interactions (reactions, suggestions) are collected per turn
/// - Turn data is saved for replay and stats
///
/// TODO: Add turn replay functionality
/// TODO: Add turn skip option (host only)
/// TODO: Implement turn timeout handling
/// </summary>
public class TurnState
{
    /// <summary>
    /// Unique identifier for this turn
    /// </summary>
    public string TurnId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Session this turn belongs to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Player who is answering (Hot Seat, Speed Round)
    /// Or team that is answering (Team Challenge)
    /// </summary>
    public string ActivePlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Current question number (1-indexed)
    /// </summary>
    public int QuestionNumber { get; set; }

    /// <summary>
    /// Current phase of the turn
    /// Determines UI and allowed actions
    /// </summary>
    public TurnPhase Phase { get; set; } = TurnPhase.Question;

    /// <summary>
    /// When this turn started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this turn ended
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Total time limit for this turn (seconds)
    /// Varies by game mode
    /// </summary>
    public int TimeLimit { get; set; } = 30;

    /// <summary>
    /// Time remaining in current phase (seconds)
    /// Decremented in real-time
    /// </summary>
    public int TimeRemaining { get; set; } = 30;

    /// <summary>
    /// Player responses for this turn
    /// In team modes, multiple players may respond
    /// </summary>
    public List<PlayerResponse> Responses { get; set; } = new();

    /// <summary>
    /// Reactions received during this turn
    /// From spectators or team members
    /// </summary>
    public List<Reaction> Reactions { get; set; } = new();

    /// <summary>
    /// Suggestions sent to active player
    /// Anonymous, revealed after answer
    /// </summary>
    public List<Suggestion> Suggestions { get; set; } = new();

    /// <summary>
    /// Predictions from spectators (what will player pick)
    /// Used for engagement and bonus points
    /// </summary>
    public List<Prediction> Predictions { get; set; } = new();

    /// <summary>
    /// Score earned this turn
    /// Calculated based on game mode rules
    /// </summary>
    public int ScoreEarned { get; set; } = 0;

    /// <summary>
    /// Score breakdown for this turn
    /// Shows how score was calculated
    /// </summary>
    public ScoreBreakdown TurnScoreBreakdown { get; set; } = new();

    /// <summary>
    /// Whether this turn was explicitly marked as timed out
    /// Set by the timer service when time expires
    /// </summary>
    public bool TimedOut { get; set; } = false;

    // Helper properties

    /// <summary>
    /// Duration of this turn
    /// </summary>
    public TimeSpan Duration =>
        EndTime.HasValue
            ? EndTime.Value - StartTime
            : DateTime.UtcNow - StartTime;

    /// <summary>
    /// Is turn timed out? (computed or explicit)
    /// </summary>
    public bool IsTimedOut =>
        TimedOut || DateTime.UtcNow - StartTime > TimeSpan.FromSeconds(TimeLimit);

    /// <summary>
    /// Has active player answered?
    /// </summary>
    public bool HasAnswer =>
        Responses.Any(r => r.PlayerId == ActivePlayerId);

    /// <summary>
    /// Get the active player's answer
    /// </summary>
    public PlayerResponse? GetActivePlayerResponse() =>
        Responses.FirstOrDefault(r => r.PlayerId == ActivePlayerId);

    // Helper methods

    /// <summary>
    /// Add a player response to this turn
    /// </summary>
    public void AddResponse(PlayerResponse response)
    {
        response.TurnId = TurnId;
        response.QuestionNumber = QuestionNumber;
        Responses.Add(response);
    }

    /// <summary>
    /// Add a reaction to this turn
    /// Enforces limits per player
    /// </summary>
    public bool AddReaction(Reaction reaction, int maxPerPlayer = 3)
    {
        int currentCount = Reactions.Count(r => r.PlayerId == reaction.PlayerId);
        if (currentCount >= maxPerPlayer)
        {
            return false; // Player has used all reactions
        }

        reaction.TurnId = TurnId;
        reaction.QuestionNumber = QuestionNumber;
        Reactions.Add(reaction);
        return true;
    }

    /// <summary>
    /// Add a suggestion (if limit not reached)
    /// </summary>
    public bool AddSuggestion(Suggestion suggestion, int maxPerPlayer = 1)
    {
        int currentCount = Suggestions.Count(s => s.PlayerId == suggestion.PlayerId);
        if (currentCount >= maxPerPlayer)
        {
            return false;
        }

        suggestion.TurnId = TurnId;
        suggestion.QuestionNumber = QuestionNumber;
        Suggestions.Add(suggestion);
        return true;
    }

    /// <summary>
    /// Transition to next phase
    /// </summary>
    public void NextPhase()
    {
        Phase = Phase switch
        {
            TurnPhase.Question => TurnPhase.Answering,
            TurnPhase.Answering => TurnPhase.Reaction,
            TurnPhase.Reaction => TurnPhase.Results,
            TurnPhase.Results => TurnPhase.Complete,
            _ => TurnPhase.Complete
        };
    }

    // TODO: Add CalculateScore() method per game mode
    // TODO: Add GetTopReaction() - most used emoji
    // TODO: Add GetCorrectPredictionCount()
    // TODO: Add ExportTurnData() for highlights
}

/// <summary>
/// Turn lifecycle phases
/// Determines UI display and allowed actions
/// </summary>
public enum TurnPhase
{
    /// <summary>
    /// Question is displayed, timer not started
    /// Players can read question
    /// </summary>
    Question,

    /// <summary>
    /// Timer running, active player can answer
    /// Spectators can react and suggest
    /// </summary>
    Answering,

    /// <summary>
    /// Answer submitted, brief reaction time
    /// Show what was picked, allow reactions
    /// Duration: 3-5 seconds
    /// </summary>
    Reaction,

    /// <summary>
    /// Show results, score calculation
    /// Display correctness, points earned, leaderboard update
    /// Duration: 5-8 seconds
    /// </summary>
    Results,

    /// <summary>
    /// Turn complete, transitioning to next
    /// </summary>
    Complete
}

/// <summary>
/// Individual player response within a turn
///
/// TODO: Add response confidence level
/// TODO: Add time to make decision
/// TODO: Track answer change history (if allowed)
/// </summary>
public class PlayerResponse
{
    public string ResponseId { get; set; } = Guid.NewGuid().ToString();
    public string TurnId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public int QuestionNumber { get; set; }

    /// <summary>
    /// Selected answer index (0-based)
    /// </summary>
    public int AnswerIndex { get; set; }

    /// <summary>
    /// Selected answer text
    /// </summary>
    public string AnswerText { get; set; } = string.Empty;

    /// <summary>
    /// When answer was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Response time from question shown to answer submitted (seconds)
    /// </summary>
    public double ResponseTime { get; set; }

    /// <summary>
    /// Was this answer correct? (based on AI personality match)
    /// Null if not yet evaluated
    /// </summary>
    public bool? IsCorrect { get; set; }

    /// <summary>
    /// Score earned from this response
    /// </summary>
    public int PointsEarned { get; set; } = 0;

    /// <summary>
    /// Did player follow a suggestion?
    /// </summary>
    public bool FollowedSuggestion { get; set; } = false;

    /// <summary>
    /// Which suggestion was followed (if any)
    /// </summary>
    public string? SuggestionId { get; set; }

    // TODO: Add AnswerConfidence (0-100%)
    // TODO: Add TimeUnderPressure (if < 5s remaining)
}
