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
///
/// TODO: Add pause/resume endpoints
/// TODO: Add spectator join endpoint
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
