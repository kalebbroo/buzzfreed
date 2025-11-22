using Microsoft.AspNetCore.SignalR;
using BuzzFreed.Web.Hubs;
using BuzzFreed.Web.Models;
using BuzzFreed.Web.Models.Multiplayer;
using BuzzFreed.Web.Models.Multiplayer.GameModes;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Services.Multiplayer;

/// <summary>
/// Centralized service for broadcasting real-time events to players
///
/// DESIGN PRINCIPLES:
/// - Single responsibility: only handles event broadcasting
/// - Type-safe event payloads
/// - Consistent error handling
/// - Logging for debugging
///
/// EVENT CATEGORIES:
/// 1. Room Events - player joins/leaves, settings changes, game start
/// 2. Session Events - turn start/end, answers, scores, game end
/// 3. Interaction Events - reactions, predictions, chat
/// 4. System Events - errors, disconnections, reconnections
/// </summary>
public class BroadcastService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<BroadcastService> _logger;

    public BroadcastService(IHubContext<GameHub> hubContext, ILogger<BroadcastService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    #region Room Events

    /// <summary>
    /// Broadcast when a new room is created
    /// </summary>
    public async Task BroadcastRoomCreatedAsync(GameRoom room)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("RoomCreated", new
            {
                roomId = room.RoomId,
                roomCode = room.RoomCode,
                hostId = room.HostUserId,
                gameMode = room.GameMode.ToString(),
                maxPlayers = room.MaxPlayers,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Room created {room.RoomCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast room created event");
        }
    }

    /// <summary>
    /// Broadcast when a player joins the room
    /// </summary>
    public async Task BroadcastPlayerJoinedAsync(string roomId, Player player)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("PlayerJoined", new
            {
                roomId,
                player = new
                {
                    userId = player.UserId,
                    username = player.Username,
                    avatarUrl = player.AvatarUrl,
                    isHost = player.IsHost,
                    isReady = player.IsReady
                },
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Player {player.Username} joined room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast player joined event");
        }
    }

    /// <summary>
    /// Broadcast when a player leaves the room
    /// </summary>
    public async Task BroadcastPlayerLeftAsync(string roomId, string playerId, string username, string? reason = null)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("PlayerLeft", new
            {
                roomId,
                playerId,
                username,
                reason = reason ?? "left",
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Player {username} left room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast player left event");
        }
    }

    /// <summary>
    /// Broadcast when host changes
    /// </summary>
    public async Task BroadcastHostChangedAsync(string roomId, string newHostId, string newHostUsername)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("HostChanged", new
            {
                roomId,
                newHostId,
                newHostUsername,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: New host {newHostUsername} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast host changed event");
        }
    }

    /// <summary>
    /// Broadcast when player ready state changes
    /// </summary>
    public async Task BroadcastReadyStateChangedAsync(string roomId, string playerId, bool isReady)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("ReadyStateChanged", new
            {
                roomId,
                playerId,
                isReady,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Player {playerId} ready={isReady} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast ready state changed event");
        }
    }

    /// <summary>
    /// Broadcast when room settings change
    /// </summary>
    public async Task BroadcastSettingsChangedAsync(string roomId, QuizCustomization settings)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("SettingsChanged", new
            {
                roomId,
                settings = new
                {
                    topic = settings.Topic,
                    category = settings.Category,
                    style = settings.Style.ToString(),
                    difficulty = settings.Difficulty.ToString(),
                    questionCount = settings.QuestionCount,
                    includeImages = settings.IncludeImages
                },
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Settings changed in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast settings changed event");
        }
    }

    /// <summary>
    /// Broadcast when a team is created
    /// </summary>
    public async Task BroadcastTeamCreatedAsync(string roomId, Team team)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("TeamCreated", new
            {
                roomId,
                team = new
                {
                    teamId = team.TeamId,
                    name = team.Name,
                    color = team.Color,
                    captainId = team.CaptainId
                },
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Team {team.Name} created in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast team created event");
        }
    }

    /// <summary>
    /// Broadcast when player is assigned to a team
    /// </summary>
    public async Task BroadcastTeamAssignmentAsync(string roomId, string playerId, string teamId, string teamName)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("TeamAssignment", new
            {
                roomId,
                playerId,
                teamId,
                teamName,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Player {playerId} assigned to team {teamName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast team assignment event");
        }
    }

    /// <summary>
    /// Broadcast when game is starting (countdown begins)
    /// </summary>
    public async Task BroadcastGameStartingAsync(string roomId, string sessionId, int countdownSeconds)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("GameStarting", new
            {
                roomId,
                sessionId,
                countdownSeconds,
                timestamp = DateTime.UtcNow
            });

            Logs.Info($"Broadcast: Game starting in room {roomId}, session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast game starting event");
        }
    }

    /// <summary>
    /// Broadcast when room is deleted/closed
    /// </summary>
    public async Task BroadcastRoomDeletedAsync(string roomId, string reason)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("RoomDeleted", new
            {
                roomId,
                reason,
                timestamp = DateTime.UtcNow
            });

            Logs.Info($"Broadcast: Room {roomId} deleted - {reason}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast room deleted event");
        }
    }

    #endregion

    #region Session Events

    /// <summary>
    /// Broadcast when game session is created and ready
    /// </summary>
    public async Task BroadcastSessionCreatedAsync(GameSession session)
    {
        try
        {
            string roomGroup = $"room:{session.RoomId}";

            await _hubContext.Clients.Group(roomGroup).SendAsync("SessionCreated", new
            {
                sessionId = session.SessionId,
                roomId = session.RoomId,
                gameMode = session.GameMode.ToString(),
                questionCount = session.CurrentQuiz.Questions.Count,
                quizTitle = session.CurrentQuiz.Title,
                players = session.Players.Select(p => new
                {
                    userId = p.UserId,
                    username = p.Username,
                    avatarUrl = p.AvatarUrl
                }),
                timestamp = DateTime.UtcNow
            });

            Logs.Info($"Broadcast: Session {session.SessionId} created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast session created event");
        }
    }

    /// <summary>
    /// Broadcast when a new turn/question begins
    /// </summary>
    public async Task BroadcastTurnStartAsync(GameSession session, Question question, string activePlayerId)
    {
        try
        {
            string sessionGroup = $"session:{session.SessionId}";

            await _hubContext.Clients.Group(sessionGroup).SendAsync("TurnStart", new
            {
                sessionId = session.SessionId,
                turnNumber = session.CurrentTurn?.QuestionNumber ?? 1,
                totalQuestions = session.CurrentQuiz.Questions.Count,
                activePlayerId,
                question = new
                {
                    questionId = question.QuestionId,
                    text = question.Text,
                    answers = question.Answers,
                    imageUrl = question.ImageUrl,
                    category = question.Category
                    // Note: correctAnswerIndex NOT sent to prevent cheating
                },
                timeLimit = session.CurrentTurn?.TimeLimit ?? 30,
                startTime = DateTime.UtcNow,
                timestamp = DateTime.UtcNow
            });

            Logs.Info($"Broadcast: Turn {session.CurrentTurn?.QuestionNumber} started in session {session.SessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast turn start event");
        }
    }

    /// <summary>
    /// Broadcast when a player submits an answer
    /// </summary>
    public async Task BroadcastAnswerSubmittedAsync(string sessionId, string playerId, int answerIndex, bool revealAnswer = false)
    {
        try
        {
            string sessionGroup = $"session:{sessionId}";

            var payload = new Dictionary<string, object>
            {
                ["sessionId"] = sessionId,
                ["playerId"] = playerId,
                ["hasAnswered"] = true,
                ["timestamp"] = DateTime.UtcNow
            };

            // Only reveal the actual answer in certain game modes or after turn ends
            if (revealAnswer)
            {
                payload["answerIndex"] = answerIndex;
            }

            await _hubContext.Clients.Group(sessionGroup).SendAsync("AnswerSubmitted", payload);

            Logs.Debug($"Broadcast: Player {playerId} answered in session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast answer submitted event");
        }
    }

    /// <summary>
    /// Broadcast turn results when turn ends
    /// </summary>
    public async Task BroadcastTurnEndAsync(GameSession session, TurnState turn, Question question, Dictionary<string, int> turnScores)
    {
        try
        {
            string sessionGroup = $"session:{session.SessionId}";

            await _hubContext.Clients.Group(sessionGroup).SendAsync("TurnEnd", new
            {
                sessionId = session.SessionId,
                turnNumber = turn.QuestionNumber,
                correctAnswerIndex = question.CorrectAnswerIndex,
                explanation = question.Explanation,
                responses = turn.Responses.Select(r => new
                {
                    playerId = r.PlayerId,
                    answerIndex = r.AnswerIndex,
                    isCorrect = r.IsCorrect,
                    responseTime = r.ResponseTime.TotalSeconds
                }),
                turnScores = turnScores,
                currentScores = session.Scores,
                reactions = turn.Reactions.Select(r => new
                {
                    playerId = r.PlayerId,
                    type = r.Type.ToString()
                }),
                timestamp = DateTime.UtcNow
            });

            Logs.Info($"Broadcast: Turn {turn.QuestionNumber} ended in session {session.SessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast turn end event");
        }
    }

    /// <summary>
    /// Broadcast timer warning (e.g., 5 seconds remaining)
    /// </summary>
    public async Task BroadcastTimerWarningAsync(string sessionId, int secondsRemaining)
    {
        try
        {
            string sessionGroup = $"session:{sessionId}";

            await _hubContext.Clients.Group(sessionGroup).SendAsync("TimerWarning", new
            {
                sessionId,
                secondsRemaining,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: {secondsRemaining}s warning in session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast timer warning event");
        }
    }

    /// <summary>
    /// Broadcast when game session ends
    /// </summary>
    public async Task BroadcastGameEndAsync(GameSession session)
    {
        try
        {
            string sessionGroup = $"session:{session.SessionId}";

            // Calculate leaderboard
            var leaderboard = session.Scores
                .OrderByDescending(s => s.Value)
                .Select((s, index) => new
                {
                    rank = index + 1,
                    playerId = s.Key,
                    score = s.Value,
                    player = session.Players.FirstOrDefault(p => p.UserId == s.Key)
                })
                .ToList();

            await _hubContext.Clients.Group(sessionGroup).SendAsync("GameEnd", new
            {
                sessionId = session.SessionId,
                finalScores = session.Scores,
                leaderboard = leaderboard.Select(l => new
                {
                    l.rank,
                    l.playerId,
                    l.score,
                    username = l.player?.Username ?? "Unknown",
                    avatarUrl = l.player?.AvatarUrl
                }),
                winnerId = leaderboard.FirstOrDefault()?.playerId,
                stats = new
                {
                    totalQuestions = session.Stats.TotalQuestions,
                    totalAnswers = session.Stats.TotalAnswers,
                    totalReactions = session.Stats.TotalReactions,
                    duration = session.Stats.TotalDuration.TotalSeconds,
                    fastestPlayer = session.Stats.FastestPlayer,
                    mostAccuratePlayer = session.Stats.MostAccuratePlayer
                },
                timestamp = DateTime.UtcNow
            });

            Logs.Info($"Broadcast: Game ended in session {session.SessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast game end event");
        }
    }

    /// <summary>
    /// Broadcast when game is aborted
    /// </summary>
    public async Task BroadcastGameAbortedAsync(string sessionId, string reason)
    {
        try
        {
            string sessionGroup = $"session:{sessionId}";

            await _hubContext.Clients.Group(sessionGroup).SendAsync("GameAborted", new
            {
                sessionId,
                reason,
                timestamp = DateTime.UtcNow
            });

            Logs.Warning($"Broadcast: Game aborted in session {sessionId} - {reason}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast game aborted event");
        }
    }

    /// <summary>
    /// Broadcast score update (live leaderboard)
    /// </summary>
    public async Task BroadcastScoreUpdateAsync(string sessionId, Dictionary<string, int> scores)
    {
        try
        {
            string sessionGroup = $"session:{sessionId}";

            var leaderboard = scores
                .OrderByDescending(s => s.Value)
                .Select((s, index) => new
                {
                    rank = index + 1,
                    playerId = s.Key,
                    score = s.Value
                })
                .ToList();

            await _hubContext.Clients.Group(sessionGroup).SendAsync("ScoreUpdate", new
            {
                sessionId,
                scores,
                leaderboard,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Score update in session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast score update event");
        }
    }

    #endregion

    #region Interaction Events

    /// <summary>
    /// Broadcast when a player sends a reaction
    /// </summary>
    public async Task BroadcastReactionAsync(string sessionId, string playerId, string username, ReactionType reactionType)
    {
        try
        {
            string sessionGroup = $"session:{sessionId}";

            await _hubContext.Clients.Group(sessionGroup).SendAsync("Reaction", new
            {
                sessionId,
                playerId,
                username,
                reactionType = reactionType.ToString(),
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Reaction {reactionType} from {username} in session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast reaction event");
        }
    }

    /// <summary>
    /// Broadcast prediction count (not content to prevent spoilers)
    /// </summary>
    public async Task BroadcastPredictionCountAsync(string sessionId, int totalPredictions)
    {
        try
        {
            string sessionGroup = $"session:{sessionId}";

            await _hubContext.Clients.Group(sessionGroup).SendAsync("PredictionCount", new
            {
                sessionId,
                totalPredictions,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: {totalPredictions} predictions in session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast prediction count event");
        }
    }

    /// <summary>
    /// Broadcast prediction results after turn ends
    /// </summary>
    public async Task BroadcastPredictionResultsAsync(string sessionId, List<PredictionResult> results)
    {
        try
        {
            string sessionGroup = $"session:{sessionId}";

            await _hubContext.Clients.Group(sessionGroup).SendAsync("PredictionResults", new
            {
                sessionId,
                results = results.Select(r => new
                {
                    playerId = r.PlayerId,
                    username = r.Username,
                    predictedAnswerIndex = r.PredictedAnswerIndex,
                    wasCorrect = r.WasCorrect,
                    pointsEarned = r.PointsEarned
                }),
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Prediction results in session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast prediction results event");
        }
    }

    /// <summary>
    /// Broadcast team vote progress
    /// </summary>
    public async Task BroadcastTeamVoteProgressAsync(string teamId, string sessionId, int totalVotes, int requiredVotes)
    {
        try
        {
            string teamGroup = $"team:{teamId}";

            await _hubContext.Clients.Group(teamGroup).SendAsync("VoteProgress", new
            {
                teamId,
                sessionId,
                totalVotes,
                requiredVotes,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Vote progress {totalVotes}/{requiredVotes} for team {teamId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast vote progress event");
        }
    }

    /// <summary>
    /// Broadcast team answer locked
    /// </summary>
    public async Task BroadcastTeamAnswerLockedAsync(string teamId, string sessionId, int answerIndex, string lockedByPlayerId)
    {
        try
        {
            string teamGroup = $"team:{teamId}";

            await _hubContext.Clients.Group(teamGroup).SendAsync("AnswerLocked", new
            {
                teamId,
                sessionId,
                answerIndex,
                lockedByPlayerId,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Team {teamId} locked answer {answerIndex}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast answer locked event");
        }
    }

    #endregion

    #region System Events

    /// <summary>
    /// Broadcast error to specific players
    /// </summary>
    public async Task BroadcastErrorAsync(string groupName, string errorCode, string message)
    {
        try
        {
            await _hubContext.Clients.Group(groupName).SendAsync("Error", new
            {
                errorCode,
                message,
                timestamp = DateTime.UtcNow
            });

            Logs.Warning($"Broadcast: Error {errorCode} to {groupName} - {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast error event");
        }
    }

    /// <summary>
    /// Broadcast player connection status change
    /// </summary>
    public async Task BroadcastConnectionStatusAsync(string roomId, string playerId, bool isConnected)
    {
        try
        {
            await _hubContext.Clients.Group($"room:{roomId}").SendAsync("ConnectionStatus", new
            {
                roomId,
                playerId,
                isConnected,
                timestamp = DateTime.UtcNow
            });

            Logs.Debug($"Broadcast: Player {playerId} connection={isConnected} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast connection status event");
        }
    }

    #endregion
}

/// <summary>
/// Result of a player's prediction
/// </summary>
public class PredictionResult
{
    public string PlayerId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int PredictedAnswerIndex { get; set; }
    public bool WasCorrect { get; set; }
    public int PointsEarned { get; set; }
}
