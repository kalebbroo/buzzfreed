using Microsoft.AspNetCore.Mvc;
using BuzzFreed.Web.Models.Multiplayer;
using BuzzFreed.Web.Services.Multiplayer;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Controllers;

/// <summary>
/// REST API for active game session management
///
/// ENDPOINTS:
/// - GET /api/game/{sessionId} - Get session state
/// - POST /api/game/{sessionId}/answer - Submit answer
/// - POST /api/game/{sessionId}/next-turn - Advance to next turn
/// - POST /api/game/{sessionId}/end - End game
/// - GET /api/game/{sessionId}/leaderboard - Get current scores
/// - GET /api/game/{sessionId}/turn - Get current turn state
/// - POST /api/game/{sessionId}/reaction - Submit reaction
/// - POST /api/game/{sessionId}/suggestion - Submit suggestion
/// - POST /api/game/{sessionId}/prediction - Submit prediction
/// - POST /api/game/{sessionId}/chat - Send chat message
/// - POST /api/game/{sessionId}/reconnect - Reconnect to session
/// - POST /api/game/{sessionId}/disconnect - Report disconnection
/// - GET /api/game/{sessionId}/can-rejoin/{playerId} - Check reconnection eligibility
/// - GET /api/game/find-session/{playerId} - Find player's active session
/// - POST /api/game/{sessionId}/spectate - Join as spectator
/// - POST /api/game/{sessionId}/leave-spectate - Leave as spectator
/// - GET /api/game/{sessionId}/spectators - Get spectator list
/// - GET /api/game/{sessionId}/spectator-actions - Get available spectator actions
///
/// TODO: Add pause/resume endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GameController(
    GameSessionService sessionService,
    InteractionService interactionService) : ControllerBase
{
    public readonly GameSessionService SessionService = sessionService;
    public readonly InteractionService InteractionService = interactionService;

    /// <summary>
    /// Get session state
    /// </summary>
    [HttpGet("{sessionId}")]
    public ActionResult<GameSession> GetSession(string sessionId)
    {
        GameSession? session = SessionService.GetSession(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        return Ok(session);
    }

    /// <summary>
    /// Submit an answer
    /// </summary>
    [HttpPost("{sessionId}/answer")]
    public ActionResult<ApiResponse> SubmitAnswer(string sessionId, [FromBody] SubmitAnswerRequest request)
    {
        try
        {
            bool success = SessionService.SubmitAnswer(sessionId, request.PlayerId, request.AnswerIndex);

            if (!success)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Error = "Cannot submit answer (not your turn, time expired, or already answered)"
                });
            }

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Answer submitted"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error submitting answer: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// End current turn and start next
    /// </summary>
    [HttpPost("{sessionId}/next-turn")]
    public ActionResult<ApiResponse> NextTurn(string sessionId)
    {
        try
        {
            // End current turn
            bool endSuccess = SessionService.EndCurrentTurn(sessionId);

            if (!endSuccess)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Error = "Failed to end turn"
                });
            }

            // Start next turn
            bool nextSuccess = SessionService.StartNextTurn(sessionId);

            return Ok(new ApiResponse
            {
                Success = nextSuccess,
                Message = nextSuccess ? "Next turn started" : "Game complete"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error advancing turn: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// End game session
    /// </summary>
    [HttpPost("{sessionId}/end")]
    public ActionResult<ApiResponse> EndGame(string sessionId)
    {
        try
        {
            bool success = SessionService.EndSession(sessionId);

            return Ok(new ApiResponse
            {
                Success = success,
                Message = success ? "Game ended" : "Failed to end game"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error ending game: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Get current leaderboard
    /// </summary>
    [HttpGet("{sessionId}/leaderboard")]
    public ActionResult<LeaderboardResponse> GetLeaderboard(string sessionId)
    {
        GameSession? session = SessionService.GetSession(sessionId);

        if (session == null)
        {
            return NotFound();
        }

        List<LeaderboardEntry> entries = session.Scores
            .OrderByDescending(s => s.Value)
            .Select((s, index) => new LeaderboardEntry
            {
                Rank = index + 1,
                PlayerId = s.Key,
                Score = s.Value,
                PlayerName = session.Players.FirstOrDefault(p => p.UserId == s.Key)?.Username ?? "Unknown"
            })
            .ToList();

        return Ok(new LeaderboardResponse
        {
            Entries = entries,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get current turn state
    /// </summary>
    [HttpGet("{sessionId}/turn")]
    public ActionResult<TurnState> GetCurrentTurn(string sessionId)
    {
        GameSession? session = SessionService.GetSession(sessionId);

        if (session == null || session.CurrentTurn == null)
        {
            return NotFound();
        }

        return Ok(session.CurrentTurn);
    }

    /// <summary>
    /// Submit a reaction
    /// </summary>
    [HttpPost("{sessionId}/reaction")]
    public ActionResult<ApiResponse> SubmitReaction(string sessionId, [FromBody] SubmitReactionRequest request)
    {
        try
        {
            bool success = InteractionService.SubmitReaction(
                sessionId,
                request.PlayerId,
                request.ReactionType,
                request.TargetPlayerId
            );

            if (!success)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Error = "Cannot submit reaction (limit reached or not allowed in current phase)"
                });
            }

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Reaction submitted"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error submitting reaction: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Submit a suggestion
    /// </summary>
    [HttpPost("{sessionId}/suggestion")]
    public ActionResult<ApiResponse> SubmitSuggestion(string sessionId, [FromBody] SubmitSuggestionRequest request)
    {
        try
        {
            bool success = InteractionService.SubmitSuggestion(
                sessionId,
                request.PlayerId,
                request.SuggestedAnswerIndex,
                request.Reasoning,
                request.TargetPlayerId
            );

            if (!success)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Error = "Cannot submit suggestion (already submitted or not allowed)"
                });
            }

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Suggestion submitted anonymously"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error submitting suggestion: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Submit a prediction
    /// </summary>
    [HttpPost("{sessionId}/prediction")]
    public ActionResult<ApiResponse> SubmitPrediction(string sessionId, [FromBody] SubmitPredictionRequest request)
    {
        try
        {
            bool success = InteractionService.SubmitPrediction(
                sessionId,
                request.PlayerId,
                request.PredictedAnswerIndex,
                request.TargetPlayerId
            );

            if (!success)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Error = "Cannot submit prediction (not allowed)"
                });
            }

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Prediction submitted"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error submitting prediction: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Send chat message
    /// </summary>
    [HttpPost("{sessionId}/chat")]
    public ActionResult<ApiResponse> SendChatMessage(string sessionId, [FromBody] SendChatRequest request)
    {
        try
        {
            bool success = InteractionService.SubmitChatMessage(
                sessionId,
                request.PlayerId,
                request.Message,
                request.TeamId
            );

            if (!success)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Error = "Cannot send message (rate limited or invalid)"
                });
            }

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Message sent"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error sending chat: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Get reaction summary for turn
    /// </summary>
    [HttpGet("{sessionId}/turn/{turnId}/reactions")]
    public ActionResult<Dictionary<ReactionType, int>> GetReactionSummary(string sessionId, string turnId)
    {
        Dictionary<ReactionType, int> summary = InteractionService.GetReactionSummary(sessionId, turnId);
        return Ok(summary);
    }

    /// <summary>
    /// Get all interactions for a turn
    /// </summary>
    [HttpGet("{sessionId}/turn/{turnId}/interactions")]
    public ActionResult<List<Interaction>> GetTurnInteractions(string sessionId, string turnId)
    {
        List<Interaction> interactions = InteractionService.GetTurnInteractions(sessionId, turnId);
        return Ok(interactions);
    }

    /// <summary>
    /// Reconnect a player to an active game session
    /// Called when a player returns after a disconnection
    /// </summary>
    [HttpPost("{sessionId}/reconnect")]
    public ActionResult<ReconnectionResponse> ReconnectPlayer(string sessionId, [FromBody] ReconnectRequest request)
    {
        try
        {
            ReconnectionResult? result = SessionService.HandlePlayerReconnect(
                sessionId,
                request.PlayerId,
                request.ConnectionId
            );

            if (result == null)
            {
                return NotFound(new ReconnectionResponse
                {
                    Success = false,
                    Error = "Session not found or player cannot rejoin"
                });
            }

            if (!result.Success)
            {
                return BadRequest(new ReconnectionResponse
                {
                    Success = false,
                    Error = result.Message,
                    CanRejoinAsSpectator = result.CanRejoinAsSpectator
                });
            }

            Logs.Info($"Player {request.PlayerId} reconnected to session {sessionId}");

            return Ok(new ReconnectionResponse
            {
                Success = true,
                Message = result.Message,
                MissedTurns = result.MissedTurns,
                CurrentTurnNumber = result.CurrentTurnNumber,
                CurrentPhase = result.CurrentPhase,
                TimeRemainingMs = result.TimeRemainingMs
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error reconnecting player: {ex.Message}");
            return StatusCode(500, new ReconnectionResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Report a player disconnection
    /// Called by SignalR hub when connection is lost
    /// </summary>
    [HttpPost("{sessionId}/disconnect")]
    public ActionResult<ApiResponse> ReportDisconnection(string sessionId, [FromBody] DisconnectRequest request)
    {
        try
        {
            bool success = SessionService.HandlePlayerDisconnect(sessionId, request.PlayerId);

            if (!success)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Error = "Session not found or player not in session"
                });
            }

            Logs.Info($"Player {request.PlayerId} disconnected from session {sessionId}");

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Disconnection recorded"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error reporting disconnection: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Check if a player can rejoin a session
    /// </summary>
    [HttpGet("{sessionId}/can-rejoin/{playerId}")]
    public ActionResult<CanRejoinResponse> CanPlayerRejoin(string sessionId, string playerId)
    {
        try
        {
            bool canRejoin = SessionService.CanPlayerRejoin(sessionId, playerId);
            GameSession? session = SessionService.GetSession(sessionId);

            return Ok(new CanRejoinResponse
            {
                CanRejoin = canRejoin,
                SessionState = session?.State.ToString(),
                CurrentTurn = session?.CurrentTurnNumber,
                TotalTurns = session?.TotalTurns
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error checking rejoin eligibility: {ex.Message}");
            return StatusCode(500, new CanRejoinResponse { CanRejoin = false });
        }
    }

    /// <summary>
    /// Find a player's active session (for reconnection after app restart)
    /// </summary>
    [HttpGet("find-session/{playerId}")]
    public ActionResult<FindSessionResponse> FindPlayerSession(string playerId)
    {
        try
        {
            SessionInfo? sessionInfo = SessionService.GetSessionInfoForPlayer(playerId);

            if (sessionInfo == null)
            {
                return NotFound(new FindSessionResponse
                {
                    Found = false,
                    Message = "No active session found for player"
                });
            }

            return Ok(new FindSessionResponse
            {
                Found = true,
                SessionId = sessionInfo.SessionId,
                RoomId = sessionInfo.RoomId,
                GameMode = sessionInfo.GameMode,
                SessionState = sessionInfo.State,
                CurrentTurn = sessionInfo.CurrentTurn,
                TotalTurns = sessionInfo.TotalTurns,
                PlayerCount = sessionInfo.PlayerCount,
                CanRejoin = sessionInfo.CanRejoin
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error finding player session: {ex.Message}");
            return StatusCode(500, new FindSessionResponse { Found = false });
        }
    }

    // Spectator Endpoints

    /// <summary>
    /// Join a game session as a spectator
    /// Spectators can watch and interact but not participate
    /// </summary>
    [HttpPost("{sessionId}/spectate")]
    public ActionResult<SpectateResponse> JoinAsSpectator(string sessionId, [FromBody] SpectateRequest request)
    {
        try
        {
            GameSession? session = SessionService.GetSession(sessionId);

            if (session == null)
            {
                return NotFound(new SpectateResponse
                {
                    Success = false,
                    Error = "Session not found"
                });
            }

            // Check if already in session as player
            if (session.Players.Any(p => p.UserId == request.UserId))
            {
                return BadRequest(new SpectateResponse
                {
                    Success = false,
                    Error = "Already participating as a player"
                });
            }

            // Check if already spectating
            if (session.IsSpectator(request.UserId))
            {
                return Ok(new SpectateResponse
                {
                    Success = true,
                    Message = "Already spectating",
                    SpectatorCount = session.Spectators.Count
                });
            }

            // Create spectator player
            Player spectator = new Player
            {
                UserId = request.UserId,
                Username = request.Username,
                AvatarUrl = request.AvatarUrl,
                Role = PlayerRole.Spectator,
                IsConnected = true,
                JoinedAt = DateTime.UtcNow
            };

            bool added = session.AddSpectator(spectator);

            if (!added)
            {
                return BadRequest(new SpectateResponse
                {
                    Success = false,
                    Error = "Unable to join as spectator (session full or closed)"
                });
            }

            Logs.Info($"Spectator {request.Username} joined session {sessionId}");

            return Ok(new SpectateResponse
            {
                Success = true,
                Message = "Joined as spectator",
                SpectatorCount = session.Spectators.Count,
                CurrentTurn = session.CurrentTurnNumber,
                TotalTurns = session.TotalTurns,
                SessionState = session.State.ToString()
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error joining as spectator: {ex.Message}");
            return StatusCode(500, new SpectateResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Leave a game session as a spectator
    /// </summary>
    [HttpPost("{sessionId}/leave-spectate")]
    public ActionResult<ApiResponse> LeaveAsSpectator(string sessionId, [FromBody] LeaveSpectateRequest request)
    {
        try
        {
            GameSession? session = SessionService.GetSession(sessionId);

            if (session == null)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Error = "Session not found"
                });
            }

            bool removed = session.RemoveSpectator(request.UserId);

            if (!removed)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Error = "Not a spectator in this session"
                });
            }

            Logs.Info($"Spectator {request.UserId} left session {sessionId}");

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Left as spectator"
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error leaving as spectator: {ex.Message}");
            return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Get list of spectators in a session
    /// </summary>
    [HttpGet("{sessionId}/spectators")]
    public ActionResult<SpectatorListResponse> GetSpectators(string sessionId)
    {
        try
        {
            GameSession? session = SessionService.GetSession(sessionId);

            if (session == null)
            {
                return NotFound(new SpectatorListResponse
                {
                    Spectators = new List<SpectatorInfo>()
                });
            }

            List<SpectatorInfo> spectators = session.Spectators
                .Select(s => new SpectatorInfo
                {
                    UserId = s.UserId,
                    Username = s.Username,
                    AvatarUrl = s.AvatarUrl,
                    JoinedAt = s.JoinedAt
                })
                .ToList();

            return Ok(new SpectatorListResponse
            {
                SessionId = sessionId,
                Spectators = spectators,
                TotalCount = spectators.Count,
                MaxSpectators = session.MaxSpectators
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error getting spectators: {ex.Message}");
            return StatusCode(500, new SpectatorListResponse { Spectators = new List<SpectatorInfo>() });
        }
    }

    /// <summary>
    /// Get available spectator actions for current game state
    /// </summary>
    [HttpGet("{sessionId}/spectator-actions")]
    public ActionResult<SpectatorActionsResponse> GetSpectatorActions(string sessionId)
    {
        try
        {
            GameSession? session = SessionService.GetSession(sessionId);

            if (session == null)
            {
                return NotFound(new SpectatorActionsResponse
                {
                    AvailableActions = new List<string>()
                });
            }

            // Determine available actions based on game state
            List<string> actions = new() { "react", "predict", "chat" };

            if (session.CurrentTurn?.Phase == TurnPhase.Question)
            {
                actions.Add("suggest");
            }

            return Ok(new SpectatorActionsResponse
            {
                SessionId = sessionId,
                CurrentPhase = session.CurrentTurn?.Phase.ToString() ?? "Waiting",
                AvailableActions = actions,
                ReactionTypes = Enum.GetNames<ReactionType>().ToList()
            });
        }
        catch (Exception ex)
        {
            Logs.Error($"Error getting spectator actions: {ex.Message}");
            return StatusCode(500, new SpectatorActionsResponse { AvailableActions = new List<string>() });
        }
    }
}

// Request DTOs

public class SubmitAnswerRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public int AnswerIndex { get; set; }
}

public class SubmitReactionRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public ReactionType ReactionType { get; set; }
    public string TargetPlayerId { get; set; } = string.Empty;
}

public class SubmitSuggestionRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public int SuggestedAnswerIndex { get; set; }
    public string? Reasoning { get; set; }
    public string TargetPlayerId { get; set; } = string.Empty;
}

public class SubmitPredictionRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public int PredictedAnswerIndex { get; set; }
    public string TargetPlayerId { get; set; } = string.Empty;
}

public class SendChatRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TeamId { get; set; }
}

// Response DTOs

public class LeaderboardResponse
{
    public List<LeaderboardEntry> Entries { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class LeaderboardEntry
{
    public int Rank { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int Score { get; set; }
}

// Reconnection Request DTOs

public class ReconnectRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
}

public class DisconnectRequest
{
    public string PlayerId { get; set; } = string.Empty;
}

// Reconnection Response DTOs

public class ReconnectionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public int MissedTurns { get; set; }
    public int CurrentTurnNumber { get; set; }
    public string? CurrentPhase { get; set; }
    public int TimeRemainingMs { get; set; }
    public bool CanRejoinAsSpectator { get; set; }
}

public class CanRejoinResponse
{
    public bool CanRejoin { get; set; }
    public string? SessionState { get; set; }
    public int? CurrentTurn { get; set; }
    public int? TotalTurns { get; set; }
}

public class FindSessionResponse
{
    public bool Found { get; set; }
    public string? Message { get; set; }
    public string? SessionId { get; set; }
    public string? RoomId { get; set; }
    public string? GameMode { get; set; }
    public string? SessionState { get; set; }
    public int CurrentTurn { get; set; }
    public int TotalTurns { get; set; }
    public int PlayerCount { get; set; }
    public bool CanRejoin { get; set; }
}

// Spectator Request DTOs

public class SpectateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class LeaveSpectateRequest
{
    public string UserId { get; set; } = string.Empty;
}

// Spectator Response DTOs

public class SpectateResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public int SpectatorCount { get; set; }
    public int CurrentTurn { get; set; }
    public int TotalTurns { get; set; }
    public string? SessionState { get; set; }
}

public class SpectatorListResponse
{
    public string? SessionId { get; set; }
    public List<SpectatorInfo> Spectators { get; set; } = new();
    public int TotalCount { get; set; }
    public int MaxSpectators { get; set; }
}

public class SpectatorInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class SpectatorActionsResponse
{
    public string? SessionId { get; set; }
    public string? CurrentPhase { get; set; }
    public List<string> AvailableActions { get; set; } = new();
    public List<string> ReactionTypes { get; set; } = new();
}
