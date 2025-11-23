namespace BuzzFreed.Web.Models.Multiplayer;

/// <summary>
/// Base class for all player interactions during gameplay
/// Interactions keep spectators engaged while not actively playing
///
/// DESIGN PHILOSOPHY:
/// - Limited interactions per player per turn (prevent spam)
/// - Anonymous where appropriate (suggestions, predictions)
/// - Tracked for stats and achievements
/// - Real-time updates via Discord SDK events
///
/// TODO: Add interaction cooldowns
/// TODO: Add interaction history per player
/// TODO: Implement interaction replay
/// </summary>
public abstract class Interaction
{
    public string InteractionId { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string TurnId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty; // Player who sent interaction
    public int QuestionNumber { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Emoji reaction from a spectator
///
/// REACTION SYSTEM:
/// - Limited to 3 reactions per player per turn
/// - Reactions are public (everyone sees who reacted)
/// - Contributes to "crowd favorite" bonus
/// - Creates engagement for spectators
///
/// AVAILABLE REACTIONS:
/// 😂 Funny - This answer is hilarious
/// 🤔 Thinking - Hmm, interesting choice
/// 😱 Shocked - Can't believe they picked that!
/// 👍 Nice - Good answer!
/// 👎 Nah - Disagree with this choice
/// ⭐ Smart - Clever answer
/// 🔥 Fire - Hot take!
/// 💀 Dead - This is killing me
///
/// TODO: Add custom reactions per room
/// TODO: Add reaction combos (multiple emojis)
/// TODO: Implement reaction leaderboards
/// </summary>
public class Reaction : Interaction
{
    /// <summary>
    /// Emoji type used for reaction
    /// </summary>
    public ReactionType Type { get; set; }

    /// <summary>
    /// Target player (who is answering)
    /// </summary>
    public string TargetPlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Is this reaction visible to active player?
    /// Typically visible after they answer
    /// </summary>
    public bool VisibleToTarget { get; set; } = true;
}

public enum ReactionType
{
    Funny,      // 😂
    Thinking,   // 🤔
    Shocked,    // 😱
    Nice,       // 👍
    Nah,        // 👎
    Smart,      // ⭐
    Fire,       // 🔥
    Dead        // 💀
}

/// <summary>
/// Anonymous suggestion sent to active player
///
/// SUGGESTION SYSTEM:
/// - Limited to 1 suggestion per player per turn
/// - Anonymous to active player (see count, not content)
/// - Revealed AFTER player answers
/// - If player follows suggestion, both earn bonus points
///
/// ENGAGEMENT STRATEGY:
/// - Keeps spectators thinking about answer
/// - Creates social moments when suggestions revealed
/// - Encourages teamwork even in solo modes
/// - "I told you so!" moments
///
/// TODO: Add suggestion voting (spectators upvote suggestions)
/// TODO: Track suggestion follow rate per player
/// TODO: Add "best suggester" achievement
/// </summary>
public class Suggestion : Interaction
{
    /// <summary>
    /// Suggested answer index (0-based)
    /// </summary>
    public int SuggestedAnswerIndex { get; set; }

    /// <summary>
    /// Optional reasoning (shown after answer)
    /// Max 100 characters
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// Was this suggestion followed?
    /// </summary>
    public bool WasFollowed { get; set; } = false;

    /// <summary>
    /// Was this suggestion correct?
    /// </summary>
    public bool? WasCorrect { get; set; }

    /// <summary>
    /// Target player (who is answering)
    /// </summary>
    public string TargetPlayerId { get; set; } = string.Empty;

    // TODO: Add upvotes from other spectators
    // TODO: Add suggestion confidence level
}

/// <summary>
/// Prediction of what active player will choose
///
/// PREDICTION SYSTEM:
/// - Spectators predict what answer player will pick
/// - Correct predictions earn bonus points
/// - Creates "I know you so well!" moments
/// - Adds competitive element for spectators
///
/// SCORING:
/// - Correct prediction: +25 points
/// - If majority predicts wrong: +50 (underdog bonus)
/// - First to predict correctly: +10 extra
///
/// USE CASES:
/// - Hot Seat mode: Predict each player's answer
/// - Guess the Player mode: Core mechanic
/// - Team Challenge: Predict team's consensus
///
/// TODO: Add prediction confidence level
/// TODO: Track prediction accuracy per player
/// TODO: Add "mind reader" achievement (5+ correct in row)
/// </summary>
public class Prediction : Interaction
{
    /// <summary>
    /// Predicted answer index (0-based)
    /// </summary>
    public int PredictedAnswerIndex { get; set; }

    /// <summary>
    /// Target player (whose answer is being predicted)
    /// </summary>
    public string TargetPlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Actual answer chosen by target
    /// Null until target answers
    /// </summary>
    public int? ActualAnswerIndex { get; set; }

    /// <summary>
    /// Was prediction correct?
    /// </summary>
    public bool IsCorrect => ActualAnswerIndex.HasValue &&
                            ActualAnswerIndex.Value == PredictedAnswerIndex;

    /// <summary>
    /// Points earned from this prediction
    /// </summary>
    public int PointsEarned { get; set; } = 0;

    // TODO: Add prediction reasoning/justification
    // TODO: Add prediction timestamp for speed bonus
}

/// <summary>
/// Chat message sent during game
/// Used in team modes for discussion
///
/// CHAT SYSTEM:
/// - Team-only channels in team modes
/// - Global chat in collaborative modes
/// - Rate limited (10 messages per minute)
/// - Profanity filter applied
///
/// TODO: Implement chat system
/// TODO: Add chat replay
/// TODO: Add chat reactions (reply with emoji)
/// </summary>
public class ChatMessage : Interaction
{
    /// <summary>
    /// Message content (max 200 characters)
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Team ID (for team-only messages)
    /// Null for global messages
    /// </summary>
    public string? TeamId { get; set; }

    /// <summary>
    /// Is this message visible to all players?
    /// </summary>
    public bool IsGlobal { get; set; } = false;

    /// <summary>
    /// Reply to another message
    /// </summary>
    public string? ReplyToMessageId { get; set; }

    // TODO: Add message reactions
    // TODO: Add message editing (5s window)
    // TODO: Add message deletion (sender only)
}

/// <summary>
/// Power-up usage in sabotage/competitive modes
///
/// POWER-UP SYSTEM:
/// - Limited power-ups per team/player
/// - Can be earned through performance
/// - Strategic timing is key
///
/// POWER-UP TYPES:
/// - Skip: Skip current question (no penalty)
/// - Double: Double points for next question
/// - Steal: Steal points from another team (if they miss)
/// - Shield: Protect from sabotage
/// - TimeFreeze: Pause timer for 10 seconds
/// - Flip: Swap two answer options
/// - Chaos: Shuffle all answer options
///
/// TODO: Implement power-up system
/// TODO: Balance power-up costs and effects
/// TODO: Add power-up animations
/// </summary>
public class PowerUpUsage : Interaction
{
    public PowerUpType Type { get; set; }

    /// <summary>
    /// Target player/team (if applicable)
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Was power-up successfully used?
    /// </summary>
    public bool WasSuccessful { get; set; } = true;

    /// <summary>
    /// Effect description for UI display
    /// </summary>
    public string Effect { get; set; } = string.Empty;
}

public enum PowerUpType
{
    Skip,
    Double,
    Steal,
    Shield,
    TimeFreeze,
    Flip,
    Chaos
}

/// <summary>
/// Vote in team-based decisions
/// Tracked separately from main team vote for analytics
/// </summary>
public class TeamVoteInteraction : Interaction
{
    public string TeamId { get; set; } = string.Empty;

    /// <summary>
    /// Answer index voted for
    /// </summary>
    public int VotedAnswerIndex { get; set; }

    /// <summary>
    /// Can player change their vote?
    /// </summary>
    public bool CanChange { get; set; } = true;

    /// <summary>
    /// Previous vote (if changed)
    /// </summary>
    public int? PreviousVote { get; set; }
}

/// <summary>
/// Game event for logging important moments
///
/// EVENTS:
/// - Game start/end
/// - Turn start/end
/// - Player joined/left
/// - Score milestone (first to 500, etc.)
/// - Achievement unlocked
/// - Funny moment (determined by reactions)
///
/// TODO: Add event categories
/// TODO: Implement highlight reel generation
/// </summary>
public class GameEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public GameEventType Type { get; set; }
    public string? PlayerId { get; set; }
    public string? TeamId { get; set; }
    public string Data { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Is this a highlight moment?
    /// (Funny, impressive, important)
    /// </summary>
    public bool IsHighlight { get; set; } = false;

    /// <summary>
    /// Highlight category
    /// </summary>
    public HighlightCategory? Category { get; set; }
}

public enum GameEventType
{
    // Game lifecycle
    GameStarted,
    GameEnded,
    GamePaused,
    GameResumed,

    // Turn/Round
    TurnStarted,
    TurnEnded,
    RoundCompleted,

    // Player actions
    PlayerJoined,
    PlayerLeft,
    PlayerAnswered,
    PlayerTimedOut,
    PlayerDisconnected,
    PlayerReconnected,

    // Team actions
    TeamVoteStarted,
    TeamVoteCompleted,
    TeamAnswered,

    // Scoring
    ScoreUpdated,
    MilestoneReached,
    LeaderChanged,

    // Interactions
    ReactionStorm, // Many reactions at once
    UnanimousDecision, // 100% agreement
    ComboAchieved, // Streak milestone

    // Special moments
    Achievement,
    FunnyMoment,
    ClutchPlay,
    Comeback
}

public enum HighlightCategory
{
    Funny,      // Hilarious answer or reaction
    Impressive, // Skillful play
    Dramatic,   // Close call, comeback
    Social,     // Great teamwork moment
    Chaos       // Sabotage, power-ups
}
