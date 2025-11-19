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
///
/// TODO: Add pause/resume endpoints
/// TODO: Add reconnection endpoint
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
