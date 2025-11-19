using Microsoft.AspNetCore.Mvc;
using BuzzFreed.Web.Models.Multiplayer;
using BuzzFreed.Web.Models.Multiplayer.GameModes;
using BuzzFreed.Web.Services.Multiplayer;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Controllers;

/// <summary>
/// REST API for game room management
///
/// ENDPOINTS:
/// - POST /api/room/create - Create new room
/// - POST /api/room/join - Join existing room by code
/// - POST /api/room/leave - Leave room
/// - PUT /api/room/{roomId}/ready - Toggle ready state
/// - PUT /api/room/{roomId}/settings - Update room settings (host only)
/// - POST /api/room/{roomId}/teams - Create teams
/// - PUT /api/room/{roomId}/teams/{teamId} - Assign player to team
/// - POST /api/room/{roomId}/start - Start game (host only)
/// - GET /api/room/{roomId} - Get room state
/// - GET /api/room/code/{roomCode} - Get room by code
/// - GET /api/room/modes - Get available game modes
///
/// TODO: Add rate limiting (prevent spam room creation)
/// TODO: Add room password validation
/// TODO: Add kick player endpoint
/// TODO: Add transfer host endpoint
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomController(RoomService roomService, GameModeRegistry gameModeRegistry) : ControllerBase
{
    public readonly RoomService RoomService = roomService;
    public readonly GameModeRegistry GameModeRegistry = gameModeRegistry;

    /// <summary>
    /// Create a new game room
    /// </summary>
    [HttpPost("create")]
    public ActionResult<CreateRoomResponse> CreateRoom([FromBody] CreateRoomRequest request)
    {
        try
        {
            Logs.Info($"API: Creating room for user {request.HostUserId}");

            GameRoom room = RoomService.CreateRoom(
                request.HostUserId,
                request.HostUsername,
                request.GuildId,
                request.GameMode
            );

            return Ok(new CreateRoomResponse
            {
                Success = true,
                RoomId = room.RoomId,
                RoomCode = room.RoomCode,
                Room = room
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error creating room: {ex.Message}");
            return StatusCode(500, new CreateRoomResponse
            {
                Success = false,
                Error = "Failed to create room"
            });
        }
    }

    /// <summary>
    /// Join an existing room by code
    /// </summary>
    [HttpPost("join")]
    public ActionResult<JoinRoomResponse> JoinRoom([FromBody] JoinRoomRequest request)
    {
        try
        {
            Logs.Info($"API: User {request.UserId} joining room {request.RoomCode}");

            GameRoom? room = RoomService.JoinRoom(
                request.RoomCode,
                request.UserId,
                request.Username
            );

            if (room == null)
            {
                return BadRequest(new JoinRoomResponse
                {
                    Success = false,
                    Error = "Room not found, full, or already started"
                });
            }

            return Ok(new JoinRoomResponse
            {
                Success = true,
                RoomId = room.RoomId,
                Room = room
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error joining room: {ex.Message}");
            return StatusCode(500, new JoinRoomResponse
            {
                Success = false,
                Error = "Failed to join room"
            });
        }
    }

    /// <summary>
    /// Leave a room
    /// </summary>
    [HttpPost("leave")]
    public ActionResult<ApiResponse> LeaveRoom([FromBody] LeaveRoomRequest request)
    {
        try
        {
            bool success = RoomService.LeaveRoom(request.RoomId, request.UserId);

            return Ok(new ApiResponse
            {
                Success = success,
                Message = success ? "Left room successfully" : "Failed to leave room"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error leaving room: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Toggle player ready state
    /// </summary>
    [HttpPut("{roomId}/ready")]
    public ActionResult<ApiResponse> SetReady(string roomId, [FromBody] SetReadyRequest request)
    {
        try
        {
            bool success = RoomService.SetPlayerReady(roomId, request.UserId, request.IsReady);

            return Ok(new ApiResponse
            {
                Success = success,
                Message = success ? "Ready state updated" : "Failed to update ready state"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error setting ready: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Update room settings (host only)
    /// </summary>
    [HttpPut("{roomId}/settings")]
    public ActionResult<ApiResponse> UpdateSettings(string roomId, [FromBody] UpdateSettingsRequest request)
    {
        try
        {
            bool success = RoomService.UpdateRoomSettings(roomId, request.UserId, request.Settings);

            if (!success)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Error = "Not authorized or room not found"
                });
            }

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Settings updated"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error updating settings: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Create teams for room
    /// </summary>
    [HttpPost("{roomId}/teams")]
    public ActionResult<ApiResponse> CreateTeams(string roomId, [FromBody] CreateTeamsRequest request)
    {
        try
        {
            bool success = RoomService.CreateTeams(roomId, request.TeamCount);

            return Ok(new ApiResponse
            {
                Success = success,
                Message = success ? $"Created {request.TeamCount} teams" : "Failed to create teams"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error creating teams: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Assign player to team
    /// </summary>
    [HttpPut("{roomId}/teams/{teamId}")]
    public ActionResult<ApiResponse> AssignTeam(string roomId, string teamId, [FromBody] AssignTeamRequest request)
    {
        try
        {
            bool success = RoomService.AssignTeam(roomId, request.UserId, teamId);

            return Ok(new ApiResponse
            {
                Success = success,
                Message = success ? "Assigned to team" : "Failed to assign team"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error assigning team: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Start game (host only)
    /// </summary>
    [HttpPost("{roomId}/start")]
    public ActionResult<StartGameResponse> StartGame(string roomId, [FromBody] StartGameRequest request)
    {
        try
        {
            string? sessionId = RoomService.StartGame(roomId, request.UserId);

            if (sessionId == null)
            {
                return BadRequest(new StartGameResponse
                {
                    Success = false,
                    Error = "Cannot start game (not host, not enough players, or not all ready)"
                });
            }

            return Ok(new StartGameResponse
            {
                Success = true,
                SessionId = sessionId
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error starting game: {ex.Message}");
            return StatusCode(500, new StartGameResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Get room state by ID
    /// </summary>
    [HttpGet("{roomId}")]
    public ActionResult<GameRoom> GetRoom(string roomId)
    {
        GameRoom? room = RoomService.GetRoom(roomId);

        if (room == null)
        {
            return NotFound();
        }

        return Ok(room);
    }

    /// <summary>
    /// Get room by code
    /// </summary>
    [HttpGet("code/{roomCode}")]
    public ActionResult<GameRoom> GetRoomByCode(string roomCode)
    {
        GameRoom? room = RoomService.GetRoomByCode(roomCode);

        if (room == null)
        {
            return NotFound();
        }

        return Ok(room);
    }

    /// <summary>
    /// Get available game modes
    /// </summary>
    [HttpGet("modes")]
    public ActionResult<List<GameModeInfo>> GetGameModes([FromQuery] int? playerCount = null)
    {
        List<GameModeInfo> modes;

        if (playerCount.HasValue)
        {
            // Filter by player count
            modes = GameModeRegistry.GetModesForPlayerCount(playerCount.Value)
                .Select(m => new GameModeInfo
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
                })
                .ToList();
        }
        else
        {
            // All modes
            modes = GameModeRegistry.GetModeInfoList();
        }

        return Ok(modes);
    }
}

// Request/Response DTOs

public class CreateRoomRequest
{
    public string HostUserId { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public GameModeType GameMode { get; set; } = GameModeType.HotSeat;
}

public class CreateRoomResponse : ApiResponse
{
    public string? RoomId { get; set; }
    public string? RoomCode { get; set; }
    public GameRoom? Room { get; set; }
}

public class JoinRoomRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

public class JoinRoomResponse : ApiResponse
{
    public string? RoomId { get; set; }
    public GameRoom? Room { get; set; }
}

public class LeaveRoomRequest
{
    public string RoomId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

public class SetReadyRequest
{
    public string UserId { get; set; } = string.Empty;
    public bool IsReady { get; set; }
}

public class UpdateSettingsRequest
{
    public string UserId { get; set; } = string.Empty;
    public QuizCustomization Settings { get; set; } = new();
}

public class CreateTeamsRequest
{
    public int TeamCount { get; set; } = 2;
}

public class AssignTeamRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class StartGameRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class StartGameResponse : ApiResponse
{
    public string? SessionId { get; set; }
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
