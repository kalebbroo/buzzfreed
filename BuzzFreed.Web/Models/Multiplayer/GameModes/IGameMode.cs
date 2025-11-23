namespace BuzzFreed.Web.Models.Multiplayer.GameModes;

/// <summary>
/// Interface for all game mode implementations
///
/// DESIGN PHILOSOPHY:
/// - Each game mode controls its own rules and flow
/// - Modes can override default scoring logic
/// - Modes define their own UI requirements
/// - Easy to add new modes without changing core system
///
/// IMPLEMENTATION NOTES:
/// - Register modes in GameModeRegistry at startup
/// - Service layer calls mode methods to handle game logic
/// - Modes are stateless - state stored in GameSession
/// - Modes validate actions based on their rules
///
/// EXAMPLE FLOW:
/// 1. Player starts turn → OnTurnStart() determines who can answer
/// 2. Player submits answer → ValidateAnswer() checks if allowed
/// 3. Turn ends → CalculateScore() applies mode-specific scoring
/// 4. Next turn → GetNextActivePlayer() determines rotation
/// </summary>
public interface IGameMode
{
    /// <summary>
    /// Unique identifier for this mode (e.g., "hot-seat", "team-challenge")
    /// Used for routing and configuration
    /// </summary>
    string ModeId { get; }

    /// <summary>
    /// Display name shown in UI
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Brief description for mode selection screen
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Minimum players required to start
    /// </summary>
    int MinPlayers { get; }

    /// <summary>
    /// Maximum players allowed
    /// </summary>
    int MaxPlayers { get; }

    /// <summary>
    /// Does this mode use teams?
    /// If true, team formation screen is shown in lobby
    /// </summary>
    bool RequiresTeams { get; }

    /// <summary>
    /// Mode-specific configuration
    /// Can include time limits, scoring multipliers, special rules
    /// </summary>
    GameModeConfig Config { get; }

    /// <summary>
    /// Called when game session starts
    /// Mode can initialize any custom state needed
    /// </summary>
    /// <param name="session">Active game session</param>
    void OnGameStart(GameSession session);

    /// <summary>
    /// Called when a turn begins
    /// Mode determines who is active, sets up turn state
    /// </summary>
    /// <param name="session">Active game session</param>
    /// <param name="questionNumber">Current question number (1-based)</param>
    void OnTurnStart(GameSession session, int questionNumber);

    /// <summary>
    /// Validate if a player can submit an answer
    /// Different modes have different rules (hot seat vs simultaneous)
    /// </summary>
    /// <param name="session">Active game session</param>
    /// <param name="playerId">Player attempting to answer</param>
    /// <returns>True if player can answer, false otherwise</returns>
    bool CanPlayerAnswer(GameSession session, string playerId);

    /// <summary>
    /// Process an answer submission
    /// Mode determines what happens (immediate scoring, voting, etc.)
    /// </summary>
    /// <param name="session">Active game session</param>
    /// <param name="playerId">Player who answered</param>
    /// <param name="answerIndex">Selected answer index</param>
    void OnAnswerSubmit(GameSession session, string playerId, int answerIndex);

    /// <summary>
    /// Calculate score for a turn based on mode rules
    /// Different modes award points differently
    /// </summary>
    /// <param name="session">Active game session</param>
    /// <param name="turn">Completed turn state</param>
    /// <returns>Dictionary of PlayerId/TeamId → points earned</returns>
    Dictionary<string, int> CalculateScore(GameSession session, TurnState turn);

    /// <summary>
    /// Called when turn ends
    /// Mode can clean up, prepare for next turn
    /// </summary>
    /// <param name="session">Active game session</param>
    void OnTurnEnd(GameSession session);

    /// <summary>
    /// Determine who should be active next turn
    /// Modes control rotation (round robin, random, etc.)
    /// </summary>
    /// <param name="session">Active game session</param>
    /// <returns>Player ID who should be active next</returns>
    string GetNextActivePlayer(GameSession session);

    /// <summary>
    /// Check if game is complete based on mode rules
    /// Most modes: all questions answered
    /// Some modes: target score reached, time limit, etc.
    /// </summary>
    /// <param name="session">Active game session</param>
    /// <returns>True if game should end</returns>
    bool IsGameComplete(GameSession session);

    /// <summary>
    /// Called when game ends
    /// Mode can calculate final scores, awards, achievements
    /// </summary>
    /// <param name="session">Completed game session</param>
    void OnGameEnd(GameSession session);

    /// <summary>
    /// Get mode-specific UI configuration
    /// Tells frontend what controls to show/hide
    /// </summary>
    /// <returns>UI configuration for this mode</returns>
    GameModeUIConfig GetUIConfig();

    /// <summary>
    /// Get available actions for spectators based on current game phase
    /// Different modes may allow different spectator interactions
    /// </summary>
    /// <param name="session">Current game session</param>
    /// <param name="phase">Current turn phase</param>
    /// <returns>List of available spectator actions</returns>
    SpectatorActions GetSpectatorActions(GameSession session, TurnPhase phase);

    // TODO: Add ValidateInteraction(type, playerId) - check if interaction allowed
    // TODO: Add OnPowerUpUsed() for sabotage mode
    // TODO: Add GetTimeLimitForTurn() - mode-specific timing
}

/// <summary>
/// Configuration for a game mode
/// </summary>
public class GameModeConfig
{
    /// <summary>
    /// Default time limit per turn (seconds)
    /// </summary>
    public int DefaultTimeLimit { get; set; } = 30;

    /// <summary>
    /// Points for correct answer
    /// </summary>
    public int CorrectAnswerPoints { get; set; } = 100;

    /// <summary>
    /// Points for incorrect answer (usually 0)
    /// </summary>
    public int IncorrectAnswerPoints { get; set; } = 0;

    /// <summary>
    /// Speed bonus? (more points for faster answers)
    /// </summary>
    public bool EnableSpeedBonus { get; set; } = false;

    /// <summary>
    /// Max speed bonus points
    /// </summary>
    public int MaxSpeedBonus { get; set; } = 50;

    /// <summary>
    /// Allow reactions during turn?
    /// </summary>
    public bool EnableReactions { get; set; } = true;

    /// <summary>
    /// Allow suggestions?
    /// </summary>
    public bool EnableSuggestions { get; set; } = false;

    /// <summary>
    /// Allow predictions?
    /// </summary>
    public bool EnablePredictions { get; set; } = false;

    /// <summary>
    /// Allow power-ups?
    /// </summary>
    public bool EnablePowerUps { get; set; } = false;

    /// <summary>
    /// Mode-specific custom settings
    /// </summary>
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// UI configuration for a game mode
/// Tells frontend what to display
/// </summary>
public class GameModeUIConfig
{
    /// <summary>
    /// Show timer on screen?
    /// </summary>
    public bool ShowTimer { get; set; } = true;

    /// <summary>
    /// Show live scores?
    /// </summary>
    public bool ShowLiveScores { get; set; } = true;

    /// <summary>
    /// Show other players' answers in real-time?
    /// </summary>
    public bool ShowOtherAnswers { get; set; } = false;

    /// <summary>
    /// Show reaction widgets to spectators?
    /// </summary>
    public bool ShowReactionWidget { get; set; } = true;

    /// <summary>
    /// Show suggestion widget to spectators?
    /// </summary>
    public bool ShowSuggestionWidget { get; set; } = false;

    /// <summary>
    /// Show prediction widget to spectators?
    /// </summary>
    public bool ShowPredictionWidget { get; set; } = false;

    /// <summary>
    /// Show power-up UI?
    /// </summary>
    public bool ShowPowerUps { get; set; } = false;

    /// <summary>
    /// Custom UI elements for this mode
    /// </summary>
    public List<string> CustomUIElements { get; set; } = new();

    // TODO: Add LayoutType (single-player-focus, grid-view, team-split, etc.)
    // TODO: Add ResultsDisplayType (individual, team, combined, etc.)
}

/// <summary>
/// Available actions for spectators based on game mode and phase
/// </summary>
public class SpectatorActions
{
    /// <summary>
    /// Can spectators send reactions?
    /// </summary>
    public bool CanReact { get; set; } = true;

    /// <summary>
    /// Can spectators send suggestions to active player?
    /// </summary>
    public bool CanSuggest { get; set; } = false;

    /// <summary>
    /// Can spectators make predictions?
    /// </summary>
    public bool CanPredict { get; set; } = false;

    /// <summary>
    /// Can spectators chat?
    /// </summary>
    public bool CanChat { get; set; } = true;

    /// <summary>
    /// Can spectators use power-ups? (in certain modes)
    /// </summary>
    public bool CanUsePowerUps { get; set; } = false;

    /// <summary>
    /// Available reaction types for this phase
    /// </summary>
    public List<string> AvailableReactions { get; set; } = new() { "Funny", "Thinking", "Shocked", "Nice", "Nah", "Smart", "Fire", "Dead" };

    /// <summary>
    /// Maximum suggestions allowed per turn
    /// </summary>
    public int MaxSuggestions { get; set; } = 1;

    /// <summary>
    /// Maximum reactions allowed per turn
    /// </summary>
    public int MaxReactions { get; set; } = 3;

    /// <summary>
    /// Message to display to spectators
    /// </summary>
    public string? PhaseMessage { get; set; }

    /// <summary>
    /// Static helper to create default spectator actions
    /// </summary>
    public static SpectatorActions Default => new();

    /// <summary>
    /// Create spectator actions for question phase
    /// </summary>
    public static SpectatorActions ForQuestionPhase(bool allowSuggestions = true, bool allowPredictions = true) => new()
    {
        CanReact = true,
        CanSuggest = allowSuggestions,
        CanPredict = allowPredictions,
        CanChat = true,
        PhaseMessage = "Watch and interact while the player answers!"
    };

    /// <summary>
    /// Create spectator actions for results phase
    /// </summary>
    public static SpectatorActions ForResultsPhase() => new()
    {
        CanReact = true,
        CanSuggest = false,
        CanPredict = false,
        CanChat = true,
        PhaseMessage = "See how the player did!"
    };

    /// <summary>
    /// Create spectator actions for waiting phase
    /// </summary>
    public static SpectatorActions ForWaitingPhase() => new()
    {
        CanReact = false,
        CanSuggest = false,
        CanPredict = false,
        CanChat = true,
        PhaseMessage = "Get ready for the next question..."
    };
}
