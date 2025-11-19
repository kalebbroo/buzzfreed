using System.Collections.Concurrent;

namespace BuzzFreed.Web.Models.Multiplayer;

/// <summary>
/// Represents a multiplayer game room where players gather before starting a game
///
/// ARCHITECTURE NOTES:
/// - Rooms are temporary, live only in memory (use Redis in production for scaling)
/// - Each room has a unique 6-character code for easy joining
/// - Host has special privileges (start game, change settings, kick players)
/// - Room state progresses: Lobby → InProgress → Completed
///
/// TODO: Add room expiration (auto-close after 2 hours of inactivity)
/// TODO: Add password protection option for private rooms
/// TODO: Implement room persistence to database for post-game analytics
/// </summary>
public class GameRoom
{
    /// <summary>
    /// Unique identifier for this room (GUID)
    /// </summary>
    public string RoomId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-friendly room code (e.g., "ABC-123") for easy joining
    /// Generated from ROOM_CODE_CHARS to avoid confusion (no 0/O, 1/I/L)
    /// </summary>
    public string RoomCode { get; set; } = string.Empty;

    /// <summary>
    /// Discord User ID of the player who created this room
    /// Host has special privileges and is marked with ⭐ in UI
    /// </summary>
    public string HostUserId { get; set; } = string.Empty;

    /// <summary>
    /// Discord Guild ID where this room was created
    /// Used for analytics and leaderboards per-server
    /// </summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>
    /// List of all players currently in this room
    /// Thread-safe for concurrent join/leave operations
    ///
    /// TODO: Consider max players per room (8-12 suggested)
    /// TODO: Add player roles (Host, Captain, Regular)
    /// </summary>
    public ConcurrentBag<Player> Players { get; set; } = new();

    /// <summary>
    /// Maximum number of players allowed in this room
    /// Varies by game mode (Hot Seat: 2-8, Team Challenge: 4-8, etc.)
    /// </summary>
    public int MaxPlayers { get; set; } = 8;

    /// <summary>
    /// Current state of the room
    /// Controls what actions are available and UI rendering
    /// </summary>
    public RoomState State { get; set; } = RoomState.Lobby;

    /// <summary>
    /// Selected game mode for this room
    /// Can be changed in lobby, locked once game starts
    /// </summary>
    public GameModeType GameMode { get; set; } = GameModeType.HotSeat;

    /// <summary>
    /// Quiz customization settings chosen by host
    /// Includes topic, style, images, difficulty, etc.
    /// </summary>
    public QuizCustomization QuizSettings { get; set; } = new();

    /// <summary>
    /// Team assignments (only used in team-based modes)
    /// Key: TeamId, Value: Team object with players and score
    ///
    /// TODO: Auto-balance teams option
    /// TODO: Random team assignment
    /// TODO: Persistent team colors per room
    /// </summary>
    public Dictionary<string, Team>? Teams { get; set; }

    /// <summary>
    /// When this room was created
    /// Used for cleanup of old rooms
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the game started (null if still in lobby)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the game ended (null if still playing)
    /// </summary>
    public DateTime? EndedAt { get; set; }

    // Helper methods

    /// <summary>
    /// Check if room is full
    /// </summary>
    public bool IsFull() => Players.Count >= MaxPlayers;

    /// <summary>
    /// Check if user is the host
    /// </summary>
    public bool IsHost(string userId) => HostUserId == userId;

    /// <summary>
    /// Get player by user ID
    /// </summary>
    public Player? GetPlayer(string userId) =>
        Players.FirstOrDefault(p => p.UserId == userId);

    /// <summary>
    /// Check if player is in room
    /// </summary>
    public bool HasPlayer(string userId) =>
        Players.Any(p => p.UserId == userId);

    // TODO: Add GetTeam(playerId)
    // TODO: Add GetTeammates(playerId)
    // TODO: Add GetOpponents(playerId)
    // TODO: Add IsTeamMode()
    // TODO: Add GetActiveTeam() for turn-based team modes
}

/// <summary>
/// Room lifecycle states
/// </summary>
public enum RoomState
{
    /// <summary>
    /// Waiting for players, host can configure settings
    /// Players can join, choose teams, ready up
    /// </summary>
    Lobby,

    /// <summary>
    /// Game is actively being played
    /// No new players can join (TODO: Add spectator mode)
    /// </summary>
    InProgress,

    /// <summary>
    /// Game has ended, showing final results
    /// Players can view stats, start new game, or leave
    /// </summary>
    Completed,

    /// <summary>
    /// Room is being deleted/cleaned up
    /// All players should be disconnected
    /// </summary>
    Closed
}

/// <summary>
/// Game mode types - each has unique rules and flow
///
/// DESIGN PHILOSOPHY:
/// - Each mode is self-contained with own rules
/// - Modes are registered in GameModeRegistry
/// - Easy to add new modes without touching core code
/// - Each mode defines its own UI components and scoring
///
/// TODO: Move to separate GameModeType.cs file
/// TODO: Add mode metadata (description, min/max players, estimated time)
/// </summary>
public enum GameModeType
{
    /// <summary>
    /// One player answers at a time, others spectate and react
    /// Focus: Individual performance + audience engagement
    /// Players: 2-8 | Time: 5-10 min
    /// </summary>
    HotSeat,

    /// <summary>
    /// Teams compete, voting on answers together
    /// Focus: Teamwork + strategy
    /// Players: 4-8 (2+ teams) | Time: 10-15 min
    /// </summary>
    TeamChallenge,

    /// <summary>
    /// Guess which player the quiz was made for
    /// Focus: Social deduction + knowing your friends
    /// Players: 3-8 | Time: 10-15 min
    /// </summary>
    GuessThePlayer,

    /// <summary>
    /// Everyone answers simultaneously, fastest wins
    /// Focus: Quick thinking + competition
    /// Players: 2-8 | Time: 5 min
    /// </summary>
    SpeedRound,

    /// <summary>
    /// Group works together to answer, shared result
    /// Focus: Collaboration + discussion
    /// Players: 2-8 | Time: 8-12 min
    /// </summary>
    Collaborative,

    /// <summary>
    /// Players can sabotage others' quizzes (limited)
    /// Focus: Chaos + strategy + revenge
    /// Players: 3-8 | Time: 10-15 min
    /// </summary>
    Sabotage

    // TODO: Add PredictionMode - predict what others will answer
    // TODO: Add BlindfoldMode - no questions shown, only emojis
    // TODO: Add SpectatorBattle - spectators compete while one plays
}
