using Microsoft.AspNetCore.SignalR;
using BuzzFreed.Web.Models.Multiplayer;
using BuzzFreed.Web.Services.Multiplayer;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Hubs;

/// <summary>
/// SignalR Hub for real-time game communication
///
/// ARCHITECTURE:
/// - Each player connects via WebSocket to this hub
/// - Players join "groups" for their room/session
/// - Server broadcasts events to relevant groups
/// - Client-side listens for events to update UI
///
/// GROUP NAMING CONVENTION:
/// - Room lobby: "room:{roomId}"
/// - Game session: "session:{sessionId}"
/// - Team chat: "team:{teamId}"
///
/// CONNECTION LIFECYCLE:
/// 1. Player connects → OnConnectedAsync()
/// 2. Player joins room → JoinRoom()
/// 3. Game starts → JoinSession()
/// 4. Player disconnects → OnDisconnectedAsync()
/// </summary>
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly GameSessionService _sessionService;
    private readonly BroadcastService _broadcastService;

    // Track connection metadata
    private static readonly Dictionary<string, PlayerConnection> _connections = new();
    private static readonly object _lock = new();

    public GameHub(
        ILogger<GameHub> logger,
        GameSessionService sessionService,
        BroadcastService broadcastService)
    {
        _logger = logger;
        _sessionService = sessionService;
        _broadcastService = broadcastService;
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        string connectionId = Context.ConnectionId;
        Logs.Debug($"Client connected: {connectionId}");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// Handles graceful disconnection with reconnection support
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;
        PlayerConnection? connection = null;

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out connection))
            {
                Logs.Info($"Player {connection.PlayerId} disconnected from room {connection.RoomId}");
                _connections.Remove(connectionId);
            }
        }

        // Handle game session disconnection if player was in a session
        if (connection != null && !string.IsNullOrEmpty(connection.SessionId))
        {
            try
            {
                // Mark player as disconnected in the game session
                bool handled = _sessionService.HandlePlayerDisconnect(connection.SessionId, connection.PlayerId);

                if (handled)
                {
                    Logs.Info($"Player {connection.PlayerId} disconnection handled for session {connection.SessionId}");

                    // Notify other players in the session
                    await Clients.Group($"session:{connection.SessionId}").SendAsync("PlayerDisconnected", new
                    {
                        playerId = connection.PlayerId,
                        username = connection.Username,
                        sessionId = connection.SessionId,
                        timestamp = DateTime.UtcNow,
                        message = $"{connection.Username} disconnected. They have 30 seconds to reconnect."
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.Error($"Error handling session disconnection: {ex.Message}");
            }
        }

        // Notify room if player was in one
        if (connection != null && !string.IsNullOrEmpty(connection.RoomId))
        {
            await Clients.Group($"room:{connection.RoomId}").SendAsync("PlayerDisconnected", new
            {
                playerId = connection.PlayerId,
                username = connection.Username,
                roomId = connection.RoomId,
                timestamp = DateTime.UtcNow
            });
        }

        if (exception != null)
        {
            Logs.Warning($"Client disconnected with error: {exception.Message}");
        }
        else
        {
            Logs.Debug($"Client disconnected: {connectionId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Register player identity with the connection
    /// Called after OAuth authentication
    /// </summary>
    public async Task RegisterPlayer(string playerId, string username, string? avatarUrl)
    {
        string connectionId = Context.ConnectionId;

        lock (_lock)
        {
            _connections[connectionId] = new PlayerConnection
            {
                ConnectionId = connectionId,
                PlayerId = playerId,
                Username = username,
                AvatarUrl = avatarUrl,
                ConnectedAt = DateTime.UtcNow
            };
        }

        Logs.Info($"Player registered: {username} ({playerId})");

        await Clients.Caller.SendAsync("Registered", new { success = true, playerId, username });
    }

    /// <summary>
    /// Join a room's SignalR group for real-time updates
    /// </summary>
    public async Task JoinRoom(string roomId)
    {
        string connectionId = Context.ConnectionId;
        string groupName = $"room:{roomId}";

        await Groups.AddToGroupAsync(connectionId, groupName);

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                connection.RoomId = roomId;
                Logs.Info($"Player {connection.PlayerId} joined room group: {roomId}");
            }
        }

        // Notify room that player joined the SignalR group (ready for real-time updates)
        await Clients.OthersInGroup(groupName).SendAsync("PlayerConnected", new
        {
            connectionId,
            roomId,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Leave a room's SignalR group
    /// </summary>
    public async Task LeaveRoom(string roomId)
    {
        string connectionId = Context.ConnectionId;
        string groupName = $"room:{roomId}";

        await Groups.RemoveFromGroupAsync(connectionId, groupName);

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                connection.RoomId = null;
                Logs.Info($"Player {connection.PlayerId} left room group: {roomId}");
            }
        }

        // Notify room that player left
        await Clients.OthersInGroup(groupName).SendAsync("PlayerDisconnected", new
        {
            connectionId,
            roomId,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Join a game session's SignalR group
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        string connectionId = Context.ConnectionId;
        string groupName = $"session:{sessionId}";

        await Groups.AddToGroupAsync(connectionId, groupName);

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                connection.SessionId = sessionId;
                Logs.Info($"Player {connection.PlayerId} joined session group: {sessionId}");
            }
        }
    }

    /// <summary>
    /// Leave a game session's SignalR group
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        string connectionId = Context.ConnectionId;
        string groupName = $"session:{sessionId}";

        await Groups.RemoveFromGroupAsync(connectionId, groupName);

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                connection.SessionId = null;
                Logs.Info($"Player {connection.PlayerId} left session group: {sessionId}");
            }
        }
    }

    /// <summary>
    /// Join a team's SignalR group for team chat
    /// </summary>
    public async Task JoinTeam(string teamId)
    {
        string connectionId = Context.ConnectionId;
        string groupName = $"team:{teamId}";

        await Groups.AddToGroupAsync(connectionId, groupName);

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                connection.TeamId = teamId;
                Logs.Info($"Player {connection.PlayerId} joined team group: {teamId}");
            }
        }
    }

    /// <summary>
    /// Leave a team's SignalR group
    /// </summary>
    public async Task LeaveTeam(string teamId)
    {
        string connectionId = Context.ConnectionId;
        string groupName = $"team:{teamId}";

        await Groups.RemoveFromGroupAsync(connectionId, groupName);

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                connection.TeamId = null;
            }
        }
    }

    /// <summary>
    /// Reconnect a player to an active game session
    /// Called when a player returns after a disconnection
    /// </summary>
    public async Task ReconnectToSession(string sessionId, string playerId)
    {
        string connectionId = Context.ConnectionId;

        try
        {
            // Attempt to reconnect the player in the game session
            ReconnectionResult? result = _sessionService.HandlePlayerReconnect(sessionId, playerId, connectionId);

            if (result == null || !result.Success)
            {
                await Clients.Caller.SendAsync("ReconnectionFailed", new
                {
                    sessionId,
                    playerId,
                    error = result?.Message ?? "Session not found or cannot rejoin",
                    canRejoinAsSpectator = result?.CanRejoinAsSpectator ?? false
                });
                return;
            }

            // Re-join the SignalR groups
            await Groups.AddToGroupAsync(connectionId, $"session:{sessionId}");

            // Update connection tracking
            lock (_lock)
            {
                if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
                {
                    connection.SessionId = sessionId;
                    Logs.Info($"Player {playerId} reconnected to session: {sessionId}");
                }
            }

            // Notify the reconnecting player
            await Clients.Caller.SendAsync("ReconnectionSuccessful", new
            {
                sessionId,
                playerId,
                missedTurns = result.MissedTurns,
                currentTurnNumber = result.CurrentTurnNumber,
                currentPhase = result.CurrentPhase,
                timeRemainingMs = result.TimeRemainingMs,
                message = result.Message
            });

            // Notify other players in the session
            PlayerConnection? playerConnection;
            lock (_lock)
            {
                _connections.TryGetValue(connectionId, out playerConnection);
            }

            await Clients.OthersInGroup($"session:{sessionId}").SendAsync("PlayerReconnected", new
            {
                playerId,
                username = playerConnection?.Username ?? "Unknown",
                sessionId,
                timestamp = DateTime.UtcNow,
                message = $"{playerConnection?.Username ?? "A player"} reconnected!"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error during session reconnection: {ex.Message}");
            await Clients.Caller.SendAsync("ReconnectionFailed", new
            {
                sessionId,
                playerId,
                error = "An error occurred during reconnection"
            });
        }
    }

    /// <summary>
    /// Check if player has an active session to reconnect to
    /// Called on app startup/reload
    /// </summary>
    public async Task CheckForActiveSession(string playerId)
    {
        try
        {
            SessionInfo? sessionInfo = _sessionService.GetSessionInfoForPlayer(playerId);

            if (sessionInfo == null)
            {
                await Clients.Caller.SendAsync("NoActiveSession", new { playerId });
                return;
            }

            await Clients.Caller.SendAsync("ActiveSessionFound", new
            {
                playerId,
                sessionId = sessionInfo.SessionId,
                roomId = sessionInfo.RoomId,
                gameMode = sessionInfo.GameMode,
                state = sessionInfo.State,
                currentTurn = sessionInfo.CurrentTurn,
                totalTurns = sessionInfo.TotalTurns,
                playerCount = sessionInfo.PlayerCount,
                canRejoin = sessionInfo.CanRejoin
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error checking for active session: {ex.Message}");
            await Clients.Caller.SendAsync("NoActiveSession", new { playerId, error = ex.Message });
        }
    }

    /// <summary>
    /// Send a reaction during gameplay
    /// </summary>
    public async Task SendReaction(string sessionId, string reactionType)
    {
        PlayerConnection? connection;
        lock (_lock)
        {
            _connections.TryGetValue(Context.ConnectionId, out connection);
        }

        if (connection == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Not registered" });
            return;
        }

        // Broadcast reaction to all players in the session
        await Clients.Group($"session:{sessionId}").SendAsync("ReactionReceived", new
        {
            playerId = connection.PlayerId,
            username = connection.Username,
            avatarUrl = connection.AvatarUrl,
            reactionType,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send a chat message to team or all players
    /// </summary>
    public async Task SendChatMessage(string targetType, string targetId, string message)
    {
        PlayerConnection? connection;
        lock (_lock)
        {
            _connections.TryGetValue(Context.ConnectionId, out connection);
        }

        if (connection == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Not registered" });
            return;
        }

        // Rate limiting check would happen here via InteractionService
        // For now, broadcast directly
        string groupName = targetType switch
        {
            "team" => $"team:{targetId}",
            "session" => $"session:{targetId}",
            "room" => $"room:{targetId}",
            _ => throw new ArgumentException($"Invalid target type: {targetType}")
        };

        await Clients.Group(groupName).SendAsync("ChatMessage", new
        {
            playerId = connection.PlayerId,
            username = connection.Username,
            avatarUrl = connection.AvatarUrl,
            message,
            targetType,
            targetId,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get count of connected players in a room
    /// </summary>
    public int GetRoomConnectionCount(string roomId)
    {
        lock (_lock)
        {
            return _connections.Values.Count(c => c.RoomId == roomId);
        }
    }

    /// <summary>
    /// Get player connection info by playerId
    /// </summary>
    public static PlayerConnection? GetPlayerConnection(string playerId)
    {
        lock (_lock)
        {
            return _connections.Values.FirstOrDefault(c => c.PlayerId == playerId);
        }
    }

    /// <summary>
    /// Check if player is currently connected
    /// </summary>
    public static bool IsPlayerConnected(string playerId)
    {
        lock (_lock)
        {
            return _connections.Values.Any(c => c.PlayerId == playerId);
        }
    }

    // Spectator Methods

    /// <summary>
    /// Join a session as a spectator
    /// Spectators can watch and interact but not participate
    /// </summary>
    public async Task JoinAsSpectator(string sessionId, string playerId, string username, string? avatarUrl)
    {
        string connectionId = Context.ConnectionId;

        // Try to add spectator to the session
        GameSession? session = _sessionService.GetSession(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("SpectatorJoinFailed", new
            {
                sessionId,
                error = "Session not found"
            });
            return;
        }

        // Check if already a player
        if (session.Players.Any(p => p.UserId == playerId))
        {
            await Clients.Caller.SendAsync("SpectatorJoinFailed", new
            {
                sessionId,
                error = "Already participating as a player"
            });
            return;
        }

        // Create spectator
        Player spectator = new Player
        {
            UserId = playerId,
            Username = username,
            AvatarUrl = avatarUrl,
            Role = PlayerRole.Spectator,
            IsConnected = true,
            ConnectionId = connectionId,
            JoinedAt = DateTime.UtcNow
        };

        bool added = session.AddSpectator(spectator);
        if (!added)
        {
            await Clients.Caller.SendAsync("SpectatorJoinFailed", new
            {
                sessionId,
                error = "Unable to join as spectator (session full)"
            });
            return;
        }

        // Join SignalR group
        await Groups.AddToGroupAsync(connectionId, $"session:{sessionId}");
        await Groups.AddToGroupAsync(connectionId, $"spectators:{sessionId}");

        // Update connection tracking
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                connection.SessionId = sessionId;
                connection.IsSpectator = true;
                Logs.Info($"Spectator {username} joined session: {sessionId}");
            }
            else
            {
                // Create new connection if not registered
                _connections[connectionId] = new PlayerConnection
                {
                    ConnectionId = connectionId,
                    PlayerId = playerId,
                    Username = username,
                    AvatarUrl = avatarUrl,
                    SessionId = sessionId,
                    IsSpectator = true,
                    ConnectedAt = DateTime.UtcNow
                };
            }
        }

        // Notify the spectator
        await Clients.Caller.SendAsync("SpectatorJoinSuccessful", new
        {
            sessionId,
            spectatorCount = session.Spectators.Count,
            currentTurn = session.CurrentTurnNumber,
            totalTurns = session.TotalTurns,
            sessionState = session.State.ToString()
        });

        // Notify other participants
        await Clients.OthersInGroup($"session:{sessionId}").SendAsync("SpectatorJoined", new
        {
            playerId,
            username,
            avatarUrl,
            sessionId,
            spectatorCount = session.Spectators.Count,
            timestamp = DateTime.UtcNow
        });

        // Broadcast via BroadcastService
        await _broadcastService.BroadcastSessionSpectatorJoinedAsync(sessionId, spectator);
    }

    /// <summary>
    /// Leave a session as a spectator
    /// </summary>
    public async Task LeaveAsSpectator(string sessionId)
    {
        string connectionId = Context.ConnectionId;
        PlayerConnection? connection;

        lock (_lock)
        {
            _connections.TryGetValue(connectionId, out connection);
        }

        if (connection == null || !connection.IsSpectator)
        {
            return;
        }

        // Remove from session
        GameSession? session = _sessionService.GetSession(sessionId);
        if (session != null)
        {
            session.RemoveSpectator(connection.PlayerId);
        }

        // Leave SignalR groups
        await Groups.RemoveFromGroupAsync(connectionId, $"session:{sessionId}");
        await Groups.RemoveFromGroupAsync(connectionId, $"spectators:{sessionId}");

        // Update connection tracking
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out connection))
            {
                connection.SessionId = null;
                connection.IsSpectator = false;
            }
        }

        // Notify other participants
        await Clients.Group($"session:{sessionId}").SendAsync("SpectatorLeft", new
        {
            playerId = connection?.PlayerId,
            username = connection?.Username,
            sessionId,
            spectatorCount = session?.Spectators.Count ?? 0,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get count of spectators in a session
    /// </summary>
    public int GetSpectatorCount(string sessionId)
    {
        GameSession? session = _sessionService.GetSession(sessionId);
        return session?.Spectators.Count ?? 0;
    }

    /// <summary>
    /// Check if a user is spectating a session
    /// </summary>
    public bool IsSpectating(string sessionId, string playerId)
    {
        GameSession? session = _sessionService.GetSession(sessionId);
        return session?.IsSpectator(playerId) ?? false;
    }
}

/// <summary>
/// Tracks metadata about a player's SignalR connection
/// </summary>
public class PlayerConnection
{
    public string ConnectionId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? RoomId { get; set; }
    public string? SessionId { get; set; }
    public string? TeamId { get; set; }
    public bool IsSpectator { get; set; } = false;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
