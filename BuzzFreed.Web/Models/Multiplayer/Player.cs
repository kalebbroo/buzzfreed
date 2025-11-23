namespace BuzzFreed.Web.Models.Multiplayer;

/// <summary>
/// Represents a player in a multiplayer game session
///
/// DESIGN PHILOSOPHY:
/// - Players persist across multiple rooms (lifetime stats)
/// - Each player instance represents their state in ONE room
/// - Discord User ID is the primary identifier
/// - Stats are tracked for achievements and leaderboards
///
/// TODO: Add player preferences (favorite modes, quiz styles)
/// TODO: Add player titles/badges earned through play
/// TODO: Add friend system for future social features
/// </summary>
public class Player
{
    /// <summary>
    /// Discord User ID - unique identifier for this player
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Discord username for display
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Discord avatar URL
    /// Falls back to default avatar if not available
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Team this player belongs to (null if not in team mode)
    /// </summary>
    public string? TeamId { get; set; }

    /// <summary>
    /// Player role in the room
    /// </summary>
    public PlayerRole Role { get; set; } = PlayerRole.Player;

    /// <summary>
    /// Is this player ready to start the game?
    /// In lobby, all players must ready up before host can start
    /// Host ready state is implicit (they click Start)
    ///
    /// TODO: Add ready timeout (auto-ready after 60s)
    /// TODO: Add "unready" when settings change
    /// </summary>
    public bool IsReady { get; set; } = false;

    /// <summary>
    /// Is this player currently connected?
    /// Updates in real-time via Discord SDK presence
    /// </summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// Current connection state for reconnection handling
    /// </summary>
    public PlayerConnectionState ConnectionState { get; set; } = PlayerConnectionState.Connected;

    /// <summary>
    /// When player last disconnected (null if never disconnected)
    /// </summary>
    public DateTime? DisconnectedAt { get; set; }

    /// <summary>
    /// Number of times this player has disconnected in current session
    /// Used to detect unstable connections
    /// </summary>
    public int DisconnectCount { get; set; } = 0;

    /// <summary>
    /// SignalR connection ID for direct messaging
    /// Updated on each reconnect
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Player's total score in current game session
    /// Calculated differently per game mode
    /// </summary>
    public int Score { get; set; } = 0;

    /// <summary>
    /// Detailed score breakdown for transparency
    /// Shows how score was earned (speed bonus, accuracy, etc.)
    ///
    /// TODO: Make this mode-specific
    /// </summary>
    public ScoreBreakdown? ScoreBreakdown { get; set; }

    /// <summary>
    /// Player statistics within current room
    /// Tracks engagement and performance
    /// </summary>
    public PlayerRoomStats Stats { get; set; } = new();

    /// <summary>
    /// When this player joined the room
    /// </summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp for idle detection
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // Helper methods

    /// <summary>
    /// Mark player as active (called on any interaction)
    /// </summary>
    public void UpdateActivity()
    {
        LastActiveAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if player has been idle for given duration
    /// </summary>
    public bool IsIdle(TimeSpan duration) =>
        DateTime.UtcNow - LastActiveAt > duration;

    /// <summary>
    /// Add points to player's score with breakdown
    /// </summary>
    public void AddScore(int points, string reason)
    {
        Score += points;
        ScoreBreakdown ??= new ScoreBreakdown();
        ScoreBreakdown.Add(reason, points);
    }

    // TODO: Add GetAchievements()
    // TODO: Add HasAchievement(achievementId)
    // TODO: Add GetRank() for leaderboard position
    // TODO: Add GetWinRate() for stats
}

/// <summary>
/// Player roles within a room
/// Determines permissions and UI features
/// </summary>
public enum PlayerRole
{
    /// <summary>
    /// Regular player with no special privileges
    /// </summary>
    Player,

    /// <summary>
    /// Room creator with full control
    /// Can start game, change settings, kick players
    /// Marked with ⭐ in UI
    /// </summary>
    Host,

    /// <summary>
    /// Team captain (rotates each round in team modes)
    /// Has final say in team decisions
    /// Can call for team votes
    /// Marked with 👑 in UI
    /// </summary>
    Captain,

    /// <summary>
    /// Spectator (future feature)
    /// Can watch but not participate
    /// Can still react and chat
    /// </summary>
    Spectator
}

/// <summary>
/// Player connection states for reconnection handling
/// </summary>
public enum PlayerConnectionState
{
    /// <summary>
    /// Player is actively connected
    /// </summary>
    Connected,

    /// <summary>
    /// Player recently disconnected, waiting for reconnect
    /// Game continues but their turn may be skipped
    /// </summary>
    Disconnected,

    /// <summary>
    /// Player is attempting to reconnect
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Player has been disconnected too long or too many times
    /// Removed from active gameplay but can rejoin as spectator
    /// </summary>
    TimedOut,

    /// <summary>
    /// Player voluntarily left the game
    /// </summary>
    Left
}

/// <summary>
/// Player statistics for current room
/// Tracks engagement and performance metrics
///
/// TODO: Add per-mode specific stats
/// TODO: Add streak tracking (correct answers in a row)
/// TODO: Add reaction stats (emojis used, received)
/// </summary>
public class PlayerRoomStats
{
    /// <summary>
    /// Total questions answered by this player
    /// </summary>
    public int QuestionsAnswered { get; set; } = 0;

    /// <summary>
    /// Number of correct answers (based on AI personality match)
    /// </summary>
    public int CorrectAnswers { get; set; } = 0;

    /// <summary>
    /// Average response time in seconds
    /// </summary>
    public double AverageResponseTime { get; set; } = 0;

    /// <summary>
    /// Fastest response time in seconds
    /// </summary>
    public double FastestResponse { get; set; } = double.MaxValue;

    /// <summary>
    /// Number of reactions sent by this player
    /// </summary>
    public int ReactionsSent { get; set; } = 0;

    /// <summary>
    /// Number of reactions received on this player's answers
    /// </summary>
    public int ReactionsReceived { get; set; } = 0;

    /// <summary>
    /// Number of suggestions sent to other players
    /// </summary>
    public int SuggestionsSent { get; set; } = 0;

    /// <summary>
    /// Number of suggestions this player followed
    /// </summary>
    public int SuggestionsFollowed { get; set; } = 0;

    /// <summary>
    /// Accuracy percentage (0-100)
    /// </summary>
    public double AccuracyPercentage =>
        QuestionsAnswered > 0
            ? (double)CorrectAnswers / QuestionsAnswered * 100
            : 0;

    // TODO: Add StreakCurrent and StreakBest
    // TODO: Add TotalTimeInSpotlight (for Hot Seat mode)
    // TODO: Add TeamContributionScore (for team modes)
    // TODO: Add MostUsedReaction
}

/// <summary>
/// Detailed breakdown of how a player's score was earned
/// Provides transparency and interesting stats
///
/// TODO: Make this extensible per game mode
/// TODO: Add timestamp per score entry for replays
/// </summary>
public class ScoreBreakdown
{
    public Dictionary<string, int> Entries { get; set; } = new();

    public void Add(string reason, int points)
    {
        if (Entries.ContainsKey(reason))
        {
            Entries[reason] += points;
        }
        else
        {
            Entries[reason] = points;
        }
    }

    public int GetTotal() => Entries.Values.Sum();

    // Common score reasons (can be extended per mode)
    public const string CORRECT_ANSWER = "Correct Answer";
    public const string SPEED_BONUS = "Speed Bonus";
    public const string ACCURACY_BONUS = "Accuracy Bonus";
    public const string CROWD_FAVORITE = "Crowd Favorite";
    public const string STREAK_BONUS = "Streak Bonus";
    public const string TEAM_BONUS = "Team Bonus";
    public const string PREDICTION_CORRECT = "Correct Prediction";
    public const string FIRST_PLACE = "First Place";
    public const string PARTICIPATION = "Participation";

    // TODO: Add combo multipliers
    // TODO: Add achievement bonuses
}
