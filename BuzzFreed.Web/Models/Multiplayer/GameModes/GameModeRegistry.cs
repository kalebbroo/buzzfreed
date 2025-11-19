using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Models.Multiplayer.GameModes;

/// <summary>
/// Central registry for all game modes
/// Similar to AIProviderRegistry pattern
///
/// RESPONSIBILITIES:
/// - Register game mode implementations at startup
/// - Provide mode lookup by ID
/// - List available modes for UI
/// - Validate mode compatibility with room settings
///
/// USAGE:
/// 1. Register modes in Program.cs: registry.RegisterMode(new HotSeatMode())
/// 2. Get mode in service: IGameMode mode = registry.GetMode(session.GameMode)
/// 3. Execute mode logic: mode.OnTurnStart(session, questionNumber)
///
/// EXTENSIBILITY:
/// - Add new mode: Create class implementing IGameMode
/// - Register in startup: registry.RegisterMode(new CustomMode())
/// - No changes to core system needed
/// </summary>
public class GameModeRegistry
{
    public readonly Dictionary<string, IGameMode> Modes = new();

    /// <summary>
    /// Register a game mode
    /// </summary>
    public void RegisterMode(IGameMode mode)
    {
        if (Modes.ContainsKey(mode.ModeId))
        {
            Logs.Warning($"Game mode {mode.ModeId} already registered, overwriting");
        }

        Modes[mode.ModeId] = mode;
        Logs.Init($"Registered game mode: {mode.DisplayName} ({mode.ModeId})");
    }

    /// <summary>
    /// Get a game mode by ID
    /// </summary>
    public IGameMode? GetMode(string modeId)
    {
        if (Modes.TryGetValue(modeId, out IGameMode? mode))
        {
            return mode;
        }

        Logs.Error($"Game mode not found: {modeId}");
        return null;
    }

    /// <summary>
    /// Get game mode by enum type
    /// Converts GameModeType enum to string ID
    /// </summary>
    public IGameMode? GetMode(GameModeType modeType)
    {
        string modeId = ModeTypeToId(modeType);
        return GetMode(modeId);
    }

    /// <summary>
    /// Get all registered modes
    /// </summary>
    public List<IGameMode> GetAllModes()
    {
        return Modes.Values.ToList();
    }

    /// <summary>
    /// Get modes compatible with player count
    /// </summary>
    public List<IGameMode> GetModesForPlayerCount(int playerCount)
    {
        return Modes.Values
            .Where(m => playerCount >= m.MinPlayers && playerCount <= m.MaxPlayers)
            .ToList();
    }

    /// <summary>
    /// Check if mode is registered
    /// </summary>
    public bool IsModeRegistered(string modeId)
    {
        return Modes.ContainsKey(modeId);
    }

    /// <summary>
    /// Get mode information for UI
    /// </summary>
    public List<GameModeInfo> GetModeInfoList()
    {
        return Modes.Values.Select(m => new GameModeInfo
        {
            ModeId = m.ModeId,
            DisplayName = m.DisplayName,
            Description = m.Description,
            MinPlayers = m.MinPlayers,
            MaxPlayers = m.MaxPlayers,
            RequiresTeams = m.RequiresTeams,
            EnableReactions = m.Config.EnableReactions,
            EnableSuggestions = m.Config.EnableSuggestions,
            EnablePredictions = m.Config.EnablePredictions,
            EnablePowerUps = m.Config.EnablePowerUps
        }).ToList();
    }

    /// <summary>
    /// Convert GameModeType enum to mode ID string
    /// </summary>
    public static string ModeTypeToId(GameModeType modeType)
    {
        return modeType switch
        {
            GameModeType.HotSeat => "hot-seat",
            GameModeType.TeamChallenge => "team-challenge",
            GameModeType.GuessThePlayer => "guess-the-player",
            GameModeType.SpeedRound => "speed-round",
            GameModeType.Collaborative => "collaborative",
            GameModeType.Sabotage => "sabotage",
            _ => "hot-seat"
        };
    }

    /// <summary>
    /// Convert mode ID string to GameModeType enum
    /// </summary>
    public static GameModeType IdToModeType(string modeId)
    {
        return modeId switch
        {
            "hot-seat" => GameModeType.HotSeat,
            "team-challenge" => GameModeType.TeamChallenge,
            "guess-the-player" => GameModeType.GuessThePlayer,
            "speed-round" => GameModeType.SpeedRound,
            "collaborative" => GameModeType.Collaborative,
            "sabotage" => GameModeType.Sabotage,
            _ => GameModeType.HotSeat
        };
    }

    // TODO: Add mode validation (check if mode can run with current settings)
    // TODO: Add mode dependencies (some modes require certain features)
    // TODO: Add mode priority/ordering for UI display
    // TODO: Add mode tags (competitive, cooperative, party, strategic, etc.)
}

/// <summary>
/// Simplified mode information for API responses
/// </summary>
public class GameModeInfo
{
    public string ModeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public bool RequiresTeams { get; set; }
    public bool EnableReactions { get; set; }
    public bool EnableSuggestions { get; set; }
    public bool EnablePredictions { get; set; }
    public bool EnablePowerUps { get; set; }
}
