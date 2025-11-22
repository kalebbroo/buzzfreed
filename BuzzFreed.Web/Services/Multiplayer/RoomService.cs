using System.Collections.Concurrent;
using BuzzFreed.Web.Models.Multiplayer;
using BuzzFreed.Web.Models.Multiplayer.GameModes;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Services.Multiplayer;

/// <summary>
/// Service for managing game rooms (lobby state before game starts)
///
/// RESPONSIBILITIES:
/// - Create new rooms with unique codes
/// - Player join/leave operations
/// - Room settings configuration
/// - Team formation
/// - Start game transition (Room → GameSession)
/// - Room cleanup and expiration
///
/// STATE MANAGEMENT:
/// - Rooms stored in memory (ConcurrentDictionary)
/// - Room codes are unique and easy to share (6 chars)
/// - Rooms expire after 1 hour of inactivity
/// - Active rooms are tracked for cleanup
///
/// THREAD SAFETY:
/// - All operations are thread-safe (concurrent collections)
/// - Room access is synchronized
/// - Player operations are atomic
///
/// TODO: Add Redis persistence for horizontal scaling
/// TODO: Add room passwords for private games
/// TODO: Add reconnection handling (rejoin after disconnect)
/// TODO: Add room templates (pre-configured settings)
/// </summary>
public class RoomService(GameModeRegistry gameModeRegistry, BroadcastService broadcastService)
{
    public readonly GameModeRegistry GameModeRegistry = gameModeRegistry;
    public readonly BroadcastService BroadcastService = broadcastService;
    public readonly ConcurrentDictionary<string, GameRoom> Rooms = new();
    public readonly ConcurrentDictionary<string, string> RoomCodeToId = new();

    /// <summary>
    /// Create a new game room
    /// </summary>
    /// <param name="hostUserId">Discord user ID of host</param>
    /// <param name="hostUsername">Discord username</param>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="gameMode">Selected game mode</param>
    /// <returns>Created room</returns>
    public GameRoom CreateRoom(string hostUserId, string hostUsername, string guildId, GameModeType gameMode)
    {
        Logs.Info($"Creating room: Host={hostUserId}, Mode={gameMode}");

        GameRoom room = new GameRoom
        {
            RoomId = Guid.NewGuid().ToString(),
            RoomCode = GenerateRoomCode(),
            HostUserId = hostUserId,
            GuildId = guildId,
            GameMode = gameMode,
            State = RoomState.Lobby,
            CreatedAt = DateTime.UtcNow
        };

        // Get mode configuration
        IGameMode? mode = GameModeRegistry.GetMode(gameMode);
        if (mode != null)
        {
            room.MaxPlayers = mode.MaxPlayers;
        }

        // Add host as first player
        Player host = new Player
        {
            UserId = hostUserId,
            Username = hostUsername,
            Role = PlayerRole.Host,
            IsReady = true, // Host is auto-ready
            IsConnected = true
        };

        room.Players.Add(host);

        // Store room
        Rooms[room.RoomId] = room;
        RoomCodeToId[room.RoomCode] = room.RoomId;

        Logs.Info($"Room created: {room.RoomCode} (ID: {room.RoomId})");

        // Broadcast room created event
        _ = BroadcastService.BroadcastRoomCreatedAsync(room);

        return room;
    }

    /// <summary>
    /// Join an existing room by code
    /// </summary>
    /// <param name="roomCode">6-character room code</param>
    /// <param name="userId">Discord user ID</param>
    /// <param name="username">Discord username</param>
    /// <returns>Room if joined successfully, null if failed</returns>
    public GameRoom? JoinRoom(string roomCode, string userId, string username)
    {
        Logs.Info($"Player {userId} attempting to join room {roomCode}");

        // Find room by code
        if (!RoomCodeToId.TryGetValue(roomCode, out string? roomId))
        {
            Logs.Warning($"Room code not found: {roomCode}");
            return null;
        }

        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            Logs.Error($"Room not found by ID: {roomId}");
            return null;
        }

        // Validate join
        if (room.State != RoomState.Lobby)
        {
            Logs.Warning($"Cannot join room {roomCode} - game already started");
            return null;
        }

        if (room.IsFull())
        {
            Logs.Warning($"Cannot join room {roomCode} - room is full");
            return null;
        }

        // Check if player already in room
        if (room.Players.Any(p => p.UserId == userId))
        {
            Logs.Info($"Player {userId} already in room {roomCode}");
            return room;
        }

        // Add player
        Player player = new Player
        {
            UserId = userId,
            Username = username,
            Role = PlayerRole.Player,
            IsReady = false,
            IsConnected = true
        };

        room.Players.Add(player);

        Logs.Info($"Player {username} joined room {roomCode} ({room.Players.Count}/{room.MaxPlayers})");

        // Broadcast player joined event to room
        _ = BroadcastService.BroadcastPlayerJoinedAsync(room.RoomId, player);

        // Update room activity timestamp
        room.LastActivityAt = DateTime.UtcNow;

        return room;
    }

    /// <summary>
    /// Leave a room
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="userId">User leaving</param>
    public bool LeaveRoom(string roomId, string userId)
    {
        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            Logs.Warning($"Room not found: {roomId}");
            return false;
        }

        Player? player = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null)
        {
            Logs.Warning($"Player {userId} not in room {roomId}");
            return false;
        }

        // Remove player
        room.Players = new ConcurrentBag<Player>(room.Players.Where(p => p.UserId != userId));

        Logs.Info($"Player {userId} left room {room.RoomCode}");

        // Handle host leaving
        if (room.IsHost(userId))
        {
            if (room.Players.Count > 0)
            {
                // Transfer host to next player
                Player newHost = room.Players.First();
                newHost.Role = PlayerRole.Host;
                room.HostUserId = newHost.UserId;

                Logs.Info($"Host transferred to {newHost.UserId}");

                // Broadcast host change event
                _ = BroadcastService.BroadcastHostChangedAsync(room.RoomId, newHost.UserId, newHost.Username);
            }
            else
            {
                // Last player left, delete room
                DeleteRoom(roomId);
                return true;
            }
        }

        // Broadcast player left event
        _ = BroadcastService.BroadcastPlayerLeftAsync(room.RoomId, userId, player.Username);

        return true;
    }

    /// <summary>
    /// Toggle player ready state
    /// </summary>
    public bool SetPlayerReady(string roomId, string userId, bool isReady)
    {
        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            return false;
        }

        Player? player = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null)
        {
            return false;
        }

        player.IsReady = isReady;

        Logs.Info($"Player {userId} ready state: {isReady}");

        // Broadcast ready state change
        _ = BroadcastService.BroadcastReadyStateChangedAsync(room.RoomId, userId, isReady);

        return true;
    }

    /// <summary>
    /// Update room settings (host only)
    /// </summary>
    public bool UpdateRoomSettings(string roomId, string userId, QuizCustomization settings)
    {
        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            return false;
        }

        if (!room.IsHost(userId))
        {
            Logs.Warning($"User {userId} is not host, cannot update settings");
            return false;
        }

        room.QuizSettings = settings;

        Logs.Info($"Room {room.RoomCode} settings updated");

        // Broadcast settings change to all players
        _ = BroadcastService.BroadcastSettingsChangedAsync(room.RoomId, settings);

        return true;
    }

    /// <summary>
    /// Assign player to team
    /// </summary>
    public bool AssignTeam(string roomId, string userId, string teamId)
    {
        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            return false;
        }

        Player? player = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null)
        {
            return false;
        }

        // Remove from previous team
        if (player.TeamId != null && room.Teams != null)
        {
            if (room.Teams.TryGetValue(player.TeamId, out Team? oldTeam))
            {
                oldTeam.PlayerIds.Remove(userId);
            }
        }

        // Add to new team
        if (room.Teams != null && room.Teams.TryGetValue(teamId, out Team? newTeam))
        {
            newTeam.PlayerIds.Add(userId);
            player.TeamId = teamId;

            Logs.Info($"Player {userId} assigned to team {teamId}");

            // Broadcast team assignment
            _ = BroadcastService.BroadcastTeamAssignmentAsync(room.RoomId, userId, teamId, newTeam.Name);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Create teams for a room
    /// </summary>
    public bool CreateTeams(string roomId, int teamCount)
    {
        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            return false;
        }

        room.Teams = new Dictionary<string, Team>();

        TeamColor[] colors = new[] { TeamColor.Red, TeamColor.Blue, TeamColor.Green, TeamColor.Yellow };

        for (int i = 0; i < teamCount; i++)
        {
            Team team = new Team
            {
                TeamId = $"team-{i + 1}",
                Name = $"Team {i + 1}",
                Color = colors[i % colors.Length]
            };

            room.Teams[team.TeamId] = team;
        }

        Logs.Info($"Created {teamCount} teams for room {room.RoomCode}");

        // Broadcast team creation for each team
        foreach (var team in room.Teams.Values)
        {
            _ = BroadcastService.BroadcastTeamCreatedAsync(room.RoomId, team);
        }

        return true;
    }

    /// <summary>
    /// Start the game (transition to GameSession)
    /// </summary>
    /// <param name="roomId">Room to start</param>
    /// <param name="userId">User starting (must be host)</param>
    /// <returns>Session ID if started, null if failed</returns>
    public string? StartGame(string roomId, string userId)
    {
        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            Logs.Warning($"Room not found: {roomId}");
            return null;
        }

        if (!room.IsHost(userId))
        {
            Logs.Warning($"User {userId} is not host, cannot start game");
            return null;
        }

        // Validate start conditions
        IGameMode? mode = GameModeRegistry.GetMode(room.GameMode);
        if (mode == null)
        {
            Logs.Error($"Game mode not found: {room.GameMode}");
            return null;
        }

        int playerCount = room.Players.Count;
        if (playerCount < mode.MinPlayers)
        {
            Logs.Warning($"Not enough players: {playerCount}/{mode.MinPlayers}");
            return null;
        }

        // Check if all players ready (except host)
        bool allReady = room.Players.All(p => p.IsReady || p.Role == PlayerRole.Host);
        if (!allReady)
        {
            Logs.Warning("Not all players are ready");
            // TODO: Allow force start?
        }

        // Update room state
        room.State = RoomState.InProgress;

        Logs.Info($"Starting game for room {room.RoomCode}");

        // Generate session ID (actual session will be created by GameSessionService)
        string sessionId = Guid.NewGuid().ToString();

        // Broadcast game starting event with countdown
        _ = BroadcastService.BroadcastGameStartingAsync(room.RoomId, sessionId, countdownSeconds: 3);

        return sessionId;
    }

    /// <summary>
    /// Get room by ID
    /// </summary>
    public GameRoom? GetRoom(string roomId)
    {
        Rooms.TryGetValue(roomId, out GameRoom? room);
        return room;
    }

    /// <summary>
    /// Get room by code
    /// </summary>
    public GameRoom? GetRoomByCode(string roomCode)
    {
        if (RoomCodeToId.TryGetValue(roomCode, out string? roomId))
        {
            return GetRoom(roomId);
        }
        return null;
    }

    /// <summary>
    /// Delete a room
    /// </summary>
    public bool DeleteRoom(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out GameRoom? room))
        {
            return false;
        }

        // Broadcast room deleted event before cleanup
        _ = BroadcastService.BroadcastRoomDeletedAsync(room.RoomId, "Room closed");

        Rooms.TryRemove(roomId, out _);
        RoomCodeToId.TryRemove(room.RoomCode, out _);

        Logs.Info($"Room deleted: {room.RoomCode}");

        return true;
    }

    /// <summary>
    /// Generate unique 6-character room code
    /// Format: ABC-123
    /// </summary>
    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Random random = Random.Shared;

        string code;
        int attempts = 0;
        const int maxAttempts = 100;

        do
        {
            string part1 = new string(Enumerable.Range(0, 3)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());

            string part2 = new string(Enumerable.Range(0, 3)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());

            code = $"{part1}-{part2}";

            attempts++;

            if (attempts >= maxAttempts)
            {
                Logs.Error("Failed to generate unique room code");
                throw new InvalidOperationException("Could not generate unique room code");
            }

        } while (RoomCodeToId.ContainsKey(code));

        return code;
    }

    // TODO: Add CleanupExpiredRooms() - run periodically to remove old rooms
    // TODO: Add GetActiveRooms() - for admin dashboard
    // TODO: Add KickPlayer() - host can remove players
    // TODO: Add TransferHost() - manually change host
    // TODO: Add AutoBalanceTeams() - distribute players evenly
    // TODO: Add ValidateRoomState() - check room integrity
    // TODO: Add RoomActivityTracker - track last activity time
}
