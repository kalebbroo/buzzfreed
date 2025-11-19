namespace BuzzFreed.Web.Models.Multiplayer;

/// <summary>
/// Represents a team in team-based game modes
///
/// DESIGN PHILOSOPHY:
/// - Teams are temporary, exist only during one game session
/// - Team size should be balanced (2-4 players per team ideal)
/// - Captain rotates each round to keep engagement high
/// - Team chat and voting create social interaction
///
/// TODO: Add team voice channel integration (Discord)
/// TODO: Add team emoji/reaction for quick communication
/// TODO: Add team statistics (win rate, best combo, etc.)
/// </summary>
public class Team
{
    /// <summary>
    /// Unique identifier for this team (GUID)
    /// </summary>
    public string TeamId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Team display name (e.g., "Red Team", "Blue Team")
    /// Auto-generated from color, or custom
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Team color for visual identification
    /// Used in UI, scores, highlights
    ///
    /// Standard colors: Red, Blue, Green, Yellow
    /// TODO: Allow custom colors (hex codes)
    /// </summary>
    public TeamColor Color { get; set; }

    /// <summary>
    /// List of player user IDs on this team
    ///
    /// TODO: Add max team size limit (4 recommended)
    /// TODO: Add team balance enforcement (equal sizes)
    /// </summary>
    public List<string> PlayerIds { get; set; } = new();

    /// <summary>
    /// Current team captain (rotates each round)
    /// Captain has final say in team votes
    /// </summary>
    public string? CaptainId { get; set; }

    /// <summary>
    /// Team's total score in current game
    /// </summary>
    public int Score { get; set; } = 0;

    /// <summary>
    /// Score breakdown for transparency
    /// </summary>
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();

    /// <summary>
    /// Team statistics for current game
    /// </summary>
    public TeamStats Stats { get; set; } = new();

    /// <summary>
    /// Current team vote state (for team challenge mode)
    /// Tracks who voted for what
    ///
    /// TODO: Add vote time limit
    /// TODO: Add vote change option (re-vote before lock)
    /// </summary>
    public TeamVote? CurrentVote { get; set; }

    /// <summary>
    /// Team power-ups (game mode specific)
    /// Examples: Skip question, Double points, Steal
    ///
    /// TODO: Implement power-up system
    /// TODO: Each mode defines its own power-ups
    /// </summary>
    public Dictionary<string, int> PowerUps { get; set; } = new();

    // Helper methods

    /// <summary>
    /// Get number of players on this team
    /// </summary>
    public int PlayerCount => PlayerIds.Count;

    /// <summary>
    /// Check if player is on this team
    /// </summary>
    public bool HasPlayer(string userId) => PlayerIds.Contains(userId);

    /// <summary>
    /// Check if player is current captain
    /// </summary>
    public bool IsCaptain(string userId) => CaptainId == userId;

    /// <summary>
    /// Rotate captain to next player
    /// Called at start of each round
    /// </summary>
    public void RotateCaptain()
    {
        if (PlayerIds.Count == 0) return;

        int currentIndex = CaptainId != null
            ? PlayerIds.IndexOf(CaptainId)
            : -1;

        int nextIndex = (currentIndex + 1) % PlayerIds.Count;
        CaptainId = PlayerIds[nextIndex];
    }

    /// <summary>
    /// Add score to team with reason
    /// </summary>
    public void AddScore(int points, string reason)
    {
        Score += points;
        ScoreBreakdown.Add(reason, points);
    }

    /// <summary>
    /// Use a power-up (decrements count)
    /// </summary>
    public bool UsePowerUp(string powerUpType)
    {
        if (PowerUps.TryGetValue(powerUpType, out int count) && count > 0)
        {
            PowerUps[powerUpType]--;
            return true;
        }
        return false;
    }

    // TODO: Add GetTeamConsensus() - calculate agreement level
    // TODO: Add GetTeamMood() - based on reactions
    // TODO: Add GetTeamStrength() - performance metric
}

/// <summary>
/// Standard team colors for easy identification
/// Each has associated emoji and hex color
/// </summary>
public enum TeamColor
{
    Red,     // 🔴 #FF4444
    Blue,    // 🔵 #4444FF
    Green,   // 🟢 #44FF44
    Yellow,  // 🟡 #FFFF44
    Purple,  // 🟣 #AA44FF
    Orange,  // 🟠 #FF8844
    Pink,    // 🩷 #FF44AA
    Cyan     // 🩵 #44FFFF
}

/// <summary>
/// Team voting state for collaborative decisions
/// Used in Team Challenge mode and Collaborative mode
///
/// VOTING RULES:
/// - All team members can vote
/// - Captain can lock in vote early (majority rule)
/// - If no majority when time expires, captain decides
/// - Votes are hidden until all submit or time expires
///
/// TODO: Add anonymous voting option
/// TODO: Add vote weighting by player rank/stats
/// </summary>
public class TeamVote
{
    /// <summary>
    /// Question being voted on
    /// </summary>
    public int QuestionNumber { get; set; }

    /// <summary>
    /// Available answer options
    /// </summary>
    public List<string> Options { get; set; } = new();

    /// <summary>
    /// Individual votes: PlayerId → AnswerIndex
    /// </summary>
    public Dictionary<string, int> Votes { get; set; } = new();

    /// <summary>
    /// When voting started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time limit for voting (seconds)
    /// </summary>
    public int TimeLimit { get; set; } = 30;

    /// <summary>
    /// Is vote locked in by captain?
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Final team answer (set when locked)
    /// </summary>
    public int? FinalAnswer { get; set; }

    // Helper methods

    /// <summary>
    /// Get vote counts per option
    /// </summary>
    public Dictionary<int, int> GetVoteCounts()
    {
        Dictionary<int, int> counts = new();
        foreach (int vote in Votes.Values)
        {
            counts[vote] = counts.GetValueOrDefault(vote, 0) + 1;
        }
        return counts;
    }

    /// <summary>
    /// Get majority answer (most votes)
    /// Returns null if tie
    /// </summary>
    public int? GetMajority()
    {
        Dictionary<int, int> counts = GetVoteCounts();
        if (counts.Count == 0) return null;

        int maxCount = counts.Values.Max();
        List<int> maxOptions = counts.Where(kvp => kvp.Value == maxCount).Select(kvp => kvp.Key).ToList();

        return maxOptions.Count == 1 ? maxOptions[0] : null;
    }

    /// <summary>
    /// Check if all team members have voted
    /// </summary>
    public bool AllVoted(int teamSize) => Votes.Count >= teamSize;

    /// <summary>
    /// Check if vote time has expired
    /// </summary>
    public bool IsExpired() =>
        DateTime.UtcNow - StartTime > TimeSpan.FromSeconds(TimeLimit);

    /// <summary>
    /// Get consensus strength (0-100%)
    /// 100% = unanimous, 0% = evenly split
    /// </summary>
    public double GetConsensusStrength()
    {
        if (Votes.Count == 0) return 0;

        Dictionary<int, int> counts = GetVoteCounts();
        int maxVotes = counts.Values.Max();

        return (double)maxVotes / Votes.Count * 100;
    }

    // TODO: Add GetVoteHistory() for stats
    // TODO: Add ChangeVote(playerId, newAnswer) with time limit
}

/// <summary>
/// Team statistics for current game session
///
/// TODO: Add per-mode specific stats
/// TODO: Track team chemistry metrics
/// </summary>
public class TeamStats
{
    /// <summary>
    /// Total questions answered by team
    /// </summary>
    public int QuestionsAnswered { get; set; } = 0;

    /// <summary>
    /// Number of correct team answers
    /// </summary>
    public int CorrectAnswers { get; set; } = 0;

    /// <summary>
    /// Number of unanimous votes (100% agreement)
    /// </summary>
    public int UnanimousVotes { get; set; } = 0;

    /// <summary>
    /// Average time to reach consensus (seconds)
    /// </summary>
    public double AverageVoteTime { get; set; } = 0;

    /// <summary>
    /// Number of questions stolen from other teams
    /// </summary>
    public int StolenAnswers { get; set; } = 0;

    /// <summary>
    /// Number of power-ups used
    /// </summary>
    public int PowerUpsUsed { get; set; } = 0;

    /// <summary>
    /// Current winning streak
    /// </summary>
    public int CurrentStreak { get; set; } = 0;

    /// <summary>
    /// Best winning streak this game
    /// </summary>
    public int BestStreak { get; set; } = 0;

    /// <summary>
    /// Team accuracy percentage (0-100)
    /// </summary>
    public double AccuracyPercentage =>
        QuestionsAnswered > 0
            ? (double)CorrectAnswers / QuestionsAnswered * 100
            : 0;

    // TODO: Add TeamChemistryScore (based on vote agreement)
    // TODO: Add ComebackFactor (performance when behind)
    // TODO: Add ClutchFactor (performance under pressure)
}
