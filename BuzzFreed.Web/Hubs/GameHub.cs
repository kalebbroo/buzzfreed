using Microsoft.AspNetCore.SignalR;
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

    // Track connection metadata
    private static readonly Dictionary<string, PlayerConnection> _connections = new();
    private static readonly object _lock = new();

    public GameHub(ILogger<GameHub> logger)
    {
        _logger = logger;
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
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
            {
                Logs.Info($"Player {connection.PlayerId} disconnected from room {connection.RoomId}");
                _connections.Remove(connectionId);
            }
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
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
