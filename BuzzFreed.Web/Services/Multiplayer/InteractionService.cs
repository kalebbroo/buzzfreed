using BuzzFreed.Web.Models.Multiplayer;
using BuzzFreed.Web.Models.Multiplayer.GameModes;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Services.Multiplayer;

/// <summary>
/// Service for handling player interactions during gameplay
///
/// RESPONSIBILITIES:
/// - Process reactions from spectators
/// - Handle anonymous suggestions
/// - Process predictions
/// - Manage team chat messages
/// - Validate interaction limits (rate limiting)
/// - Broadcast interactions to relevant players
///
/// INTERACTION TYPES:
/// - Reaction: Emoji reactions (limited to 3 per turn)
/// - Suggestion: Anonymous tips (limited to 1 per turn)
/// - Prediction: Guess player's choice (1 per turn)
/// - Chat: Team communication (rate limited)
/// - PowerUp: Strategic abilities (mode-specific)
///
/// RATE LIMITING:
/// - Reactions: 3 per player per turn
/// - Suggestions: 1 per player per turn
/// - Predictions: 1 per player per turn
/// - Chat: 10 messages per minute per player
/// - Power-ups: Based on mode rules
///
/// REAL-TIME:
/// - Interactions broadcast immediately via events
/// - Some interactions hidden until reveal time
/// - Aggregated counts shown to active player
///
/// TODO: Add interaction cooldowns
/// TODO: Add interaction history per player
/// TODO: Implement interaction replay
/// TODO: Add profanity filter for chat
/// </summary>
public class InteractionService(
    GameSessionService sessionService,
    GameModeRegistry gameModeRegistry,
    BroadcastService broadcastService)
{
    public readonly GameSessionService SessionService = sessionService;
    public readonly GameModeRegistry GameModeRegistry = gameModeRegistry;
    public readonly BroadcastService BroadcastService = broadcastService;

    /// <summary>
    /// Submit a reaction to active player's turn
    /// </summary>
    /// <param name="sessionId">Active session</param>
    /// <param name="playerId">Player reacting</param>
    /// <param name="reactionType">Type of emoji reaction</param>
    /// <param name="targetPlayerId">Player being reacted to</param>
    /// <returns>True if reaction added, false if limit reached or invalid</returns>
    public bool SubmitReaction(string sessionId, string playerId, ReactionType reactionType, string targetPlayerId)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null || session.CurrentTurn == null)
        {
            Logs.Warning($"Cannot submit reaction - session or turn not found");
            return false;
        }

        // Get game mode config
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode == null || !mode.Config.EnableReactions)
        {
            Logs.Warning($"Reactions not enabled for mode {session.GameMode}");
            return false;
        }

        // Validate phase (reactions during Answering or Reaction phase)
        if (session.CurrentTurn.Phase != TurnPhase.Answering &&
            session.CurrentTurn.Phase != TurnPhase.Reaction)
        {
            Logs.Debug("Reactions not allowed in current phase");
            return false;
        }

        // Create reaction
        Reaction reaction = new Reaction
        {
            SessionId = sessionId,
            TurnId = session.CurrentTurn.TurnId,
            PlayerId = playerId,
            QuestionNumber = session.CurrentTurn.QuestionNumber,
            Type = reactionType,
            TargetPlayerId = targetPlayerId,
            VisibleToTarget = true // Show after answer
        };

        // Add to turn (validates limit)
        bool added = session.CurrentTurn.AddReaction(reaction, maxPerPlayer: 3);

        if (added)
        {
            // Also add to session interaction log
            session.Interactions.Add(reaction);
            session.Stats.TotalReactions++;

            Logs.Info($"Reaction added: {playerId} → {targetPlayerId} ({reactionType})");

            // Get player info for broadcast
            Player? player = session.Players.FirstOrDefault(p => p.UserId == playerId)
                ?? session.Spectators.FirstOrDefault(s => s.UserId == playerId);

            string username = player?.Username ?? "Unknown";

            // Broadcast reaction to all players in the session
            _ = BroadcastService.BroadcastReactionAsync(sessionId, playerId, username, reactionType);

            // Check for "reaction storm" (5+ reactions in last 3 seconds)
            DateTime threeSecondsAgo = DateTime.UtcNow.AddSeconds(-3);
            int recentReactions = session.CurrentTurn.Reactions
                .Count(r => r.Timestamp > threeSecondsAgo);

            if (recentReactions >= 5)
            {
                session.LogEvent(new GameEvent
                {
                    Type = GameEventType.ReactionStorm,
                    Data = $"{recentReactions} reactions in 3 seconds!",
                    IsHighlight = true,
                    Category = HighlightCategory.Social
                });
            }
        }
        else
        {
            Logs.Debug($"Reaction limit reached for player {playerId}");
        }

        return added;
    }

    /// <summary>
    /// Submit an anonymous suggestion
    /// </summary>
    /// <param name="sessionId">Active session</param>
    /// <param name="playerId">Player suggesting</param>
    /// <param name="suggestedAnswerIndex">Suggested answer</param>
    /// <param name="reasoning">Optional reasoning (max 100 chars)</param>
    /// <param name="targetPlayerId">Player answering</param>
    public bool SubmitSuggestion(string sessionId, string playerId, int suggestedAnswerIndex,
        string? reasoning, string targetPlayerId)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null || session.CurrentTurn == null)
        {
            return false;
        }

        // Get game mode config
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode == null || !mode.Config.EnableSuggestions)
        {
            Logs.Warning($"Suggestions not enabled for mode {session.GameMode}");
            return false;
        }

        // Validate phase (suggestions only during Question/Answering)
        if (session.CurrentTurn.Phase != TurnPhase.Question &&
            session.CurrentTurn.Phase != TurnPhase.Answering)
        {
            return false;
        }

        // Validate answer index
        Question question = session.CurrentQuiz.Questions[session.CurrentTurn.QuestionNumber - 1];
        if (suggestedAnswerIndex < 0 || suggestedAnswerIndex >= question.Answers.Count)
        {
            Logs.Warning($"Invalid answer index: {suggestedAnswerIndex}");
            return false;
        }

        // Truncate reasoning if too long
        if (reasoning != null && reasoning.Length > 100)
        {
            reasoning = reasoning.Substring(0, 100);
        }

        // Create suggestion
        Suggestion suggestion = new Suggestion
        {
            SessionId = sessionId,
            TurnId = session.CurrentTurn.TurnId,
            PlayerId = playerId,
            QuestionNumber = session.CurrentTurn.QuestionNumber,
            SuggestedAnswerIndex = suggestedAnswerIndex,
            Reasoning = reasoning,
            TargetPlayerId = targetPlayerId
        };

        // Add to turn (validates limit: 1 per player)
        bool added = session.CurrentTurn.AddSuggestion(suggestion, maxPerPlayer: 1);

        if (added)
        {
            session.Interactions.Add(suggestion);
            session.Stats.TotalSuggestions++;

            Logs.Info($"Suggestion added: {playerId} → {targetPlayerId} (Answer {suggestedAnswerIndex})");

            // Get current suggestion count for this turn
            int suggestionCount = session.CurrentTurn.Suggestions.Count;

            // Broadcast suggestion count to session (content hidden until reveal)
            _ = BroadcastService.BroadcastAnswerSubmittedAsync(
                sessionId,
                playerId,
                -1, // Don't reveal which answer
                revealAnswer: false
            );

            // Log event for suggestion
            session.LogEvent(new GameEvent
            {
                Type = GameEventType.PlayerAnswered, // Using as generic interaction event
                PlayerId = targetPlayerId,
                Data = $"Received suggestion #{suggestionCount}"
            });

            // Track suggestion follow rate will be done in ProcessSuggestions()
        }

        return added;
    }

    /// <summary>
    /// Submit a prediction of what player will choose
    /// </summary>
    public bool SubmitPrediction(string sessionId, string playerId, int predictedAnswerIndex, string targetPlayerId)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null || session.CurrentTurn == null)
        {
            return false;
        }

        // Get game mode config
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode == null || !mode.Config.EnablePredictions)
        {
            Logs.Warning($"Predictions not enabled for mode {session.GameMode}");
            return false;
        }

        // Validate phase
        if (session.CurrentTurn.Phase != TurnPhase.Question &&
            session.CurrentTurn.Phase != TurnPhase.Answering)
        {
            return false;
        }

        // Create prediction
        Prediction prediction = new Prediction
        {
            SessionId = sessionId,
            TurnId = session.CurrentTurn.TurnId,
            PlayerId = playerId,
            QuestionNumber = session.CurrentTurn.QuestionNumber,
            PredictedAnswerIndex = predictedAnswerIndex,
            TargetPlayerId = targetPlayerId
        };

        // Add to turn
        session.CurrentTurn.Predictions.Add(prediction);
        session.Interactions.Add(prediction);

        Logs.Info($"Prediction added: {playerId} predicts {targetPlayerId} will choose {predictedAnswerIndex}");

        // Broadcast prediction count (not content) to create anticipation
        int predictionCount = session.CurrentTurn.Predictions.Count;
        _ = BroadcastService.BroadcastPredictionCountAsync(sessionId, predictionCount);

        // Log prediction event
        session.LogEvent(new GameEvent
        {
            Type = GameEventType.PlayerAnswered, // Using as generic interaction
            PlayerId = playerId,
            Data = $"Made prediction for {targetPlayerId}"
        });

        return true;
    }

    /// <summary>
    /// Process predictions after answer revealed
    /// Calculate points for correct predictions
    /// </summary>
    public void ProcessPredictions(string sessionId, int actualAnswerIndex)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null || session.CurrentTurn == null)
        {
            return;
        }

        List<Prediction> predictions = session.CurrentTurn.Predictions;

        if (predictions.Count == 0)
        {
            return;
        }

        // Update predictions with actual answer
        foreach (Prediction prediction in predictions)
        {
            prediction.ActualAnswerIndex = actualAnswerIndex;

            if (prediction.IsCorrect)
            {
                // Award points
                int points = 25; // Base prediction points

                // Bonus for being first
                if (predictions.First() == prediction)
                {
                    points += 10;
                }

                // Bonus if majority predicted wrong (underdog bonus)
                int correctCount = predictions.Count(p => p.IsCorrect);
                if (correctCount <= predictions.Count / 3) // Less than 1/3 correct
                {
                    points += 25; // Underdog bonus
                }

                prediction.PointsEarned = points;

                // Add to player score
                Player? player = session.Players.FirstOrDefault(p => p.UserId == prediction.PlayerId);
                if (player != null)
                {
                    player.AddScore(points, $"Correct Prediction Q{session.CurrentTurn.QuestionNumber}");
                }

                Logs.Info($"Player {prediction.PlayerId} earned {points} points from prediction");
            }
        }

        // Build prediction results for broadcast
        List<PredictionResult> results = predictions.Select(p => new PredictionResult
        {
            PlayerId = p.PlayerId,
            Username = session.Players.FirstOrDefault(pl => pl.UserId == p.PlayerId)?.Username
                ?? session.Spectators.FirstOrDefault(s => s.UserId == p.PlayerId)?.Username
                ?? "Unknown",
            PredictedAnswerIndex = p.PredictedAnswerIndex,
            WasCorrect = p.IsCorrect,
            PointsEarned = p.PointsEarned
        }).ToList();

        // Broadcast prediction results to all players
        _ = BroadcastService.BroadcastPredictionResultsAsync(sessionId, results);

        // Log prediction results event
        int correctPredictions = results.Count(r => r.WasCorrect);
        session.LogEvent(new GameEvent
        {
            Type = GameEventType.PlayerAnswered, // Generic interaction event
            Data = $"{correctPredictions}/{predictions.Count} correct predictions",
            IsHighlight = correctPredictions > 0 && predictions.Count >= 3 // Highlight if multiple correct
        });

        // TODO: Update player prediction stats (accuracy, streak, etc.)
    }

    /// <summary>
    /// Process suggestions after answer revealed
    /// Award bonus if suggestion was followed
    /// </summary>
    public void ProcessSuggestions(string sessionId, int chosenAnswerIndex, string answeringPlayerId)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null || session.CurrentTurn == null)
        {
            return;
        }

        List<Suggestion> suggestions = session.CurrentTurn.Suggestions;

        if (suggestions.Count == 0)
        {
            return;
        }

        Question question = session.CurrentQuiz.Questions[session.CurrentTurn.QuestionNumber - 1];
        bool answerWasCorrect = (chosenAnswerIndex == question.CorrectAnswerIndex);

        foreach (Suggestion suggestion in suggestions)
        {
            suggestion.WasCorrect = (suggestion.SuggestedAnswerIndex == question.CorrectAnswerIndex);
            suggestion.WasFollowed = (suggestion.SuggestedAnswerIndex == chosenAnswerIndex);

            // Award bonus if suggestion was followed AND correct
            if (suggestion.WasFollowed && suggestion.WasCorrect == true)
            {
                int bonusPoints = 50; // Split between suggester and answerer

                // Award to suggester
                Player? suggester = session.Players.FirstOrDefault(p => p.UserId == suggestion.PlayerId);
                if (suggester != null)
                {
                    suggester.AddScore(bonusPoints / 2, "Suggestion Bonus");
                }

                // Award to answerer
                Player? answerer = session.Players.FirstOrDefault(p => p.UserId == answeringPlayerId);
                if (answerer != null)
                {
                    answerer.AddScore(bonusPoints / 2, "Followed Suggestion");
                }

                Logs.Info($"Suggestion bonus awarded: {suggestion.PlayerId} & {answeringPlayerId}");
            }
        }

        // Reveal suggestions to all players now that answer is submitted
        foreach (Suggestion suggestion in suggestions)
        {
            session.LogEvent(new GameEvent
            {
                Type = GameEventType.PlayerAnswered, // Generic interaction
                PlayerId = suggestion.PlayerId,
                Data = $"Suggested answer {suggestion.SuggestedAnswerIndex}" +
                    (suggestion.WasFollowed ? " [FOLLOWED]" : "") +
                    (suggestion.WasCorrect == true ? " [CORRECT]" : ""),
                IsHighlight = suggestion.WasFollowed && suggestion.WasCorrect == true,
                Category = suggestion.WasFollowed && suggestion.WasCorrect == true
                    ? HighlightCategory.Social
                    : null
            });
        }

        // Log summary
        int followedCount = suggestions.Count(s => s.WasFollowed);
        int correctCount = suggestions.Count(s => s.WasCorrect == true);

        Logs.Info($"Suggestions processed: {suggestions.Count} total, {followedCount} followed, {correctCount} correct");

        // TODO: Track suggestion follow rate per player
    }

    /// <summary>
    /// Submit a chat message (team modes)
    /// </summary>
    public bool SubmitChatMessage(string sessionId, string playerId, string message, string? teamId = null)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null)
        {
            return false;
        }

        // Validate message
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200)
        {
            return false;
        }

        // Check rate limit
        // TODO: Implement rate limiting (10 messages per minute)

        // TODO: Apply profanity filter
        // message = ProfanityFilter.Filter(message);

        ChatMessage chatMessage = new ChatMessage
        {
            SessionId = sessionId,
            PlayerId = playerId,
            Message = message,
            TeamId = teamId,
            IsGlobal = teamId == null
        };

        session.Interactions.Add(chatMessage);

        Logs.Info($"Chat message from {playerId}: {message}");

        // Note: Chat is typically broadcast via SignalR Hub's SendChatMessage method
        // This service validates and stores the message in session history
        // The Hub handles real-time delivery to appropriate recipients

        // Track chat activity stats
        Player? player = session.Players.FirstOrDefault(p => p.UserId == playerId)
            ?? session.Spectators.FirstOrDefault(s => s.UserId == playerId);

        if (player != null)
        {
            player.UpdateActivity();
        }

        // Log chat event for session history
        session.LogEvent(new GameEvent
        {
            Type = GameEventType.PlayerAnswered, // Using as generic player action
            PlayerId = playerId,
            Data = teamId != null ? $"Team chat ({teamId})" : "Global chat"
        });

        return true;
    }

    /// <summary>
    /// Get all interactions for a turn
    /// </summary>
    public List<Interaction> GetTurnInteractions(string sessionId, string turnId)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null)
        {
            return new List<Interaction>();
        }

        return session.Interactions
            .Where(i => i.TurnId == turnId)
            .ToList();
    }

    /// <summary>
    /// Get reaction summary for a turn
    /// </summary>
    public Dictionary<ReactionType, int> GetReactionSummary(string sessionId, string turnId)
    {
        GameSession? session = SessionService.GetSession(sessionId);
        if (session == null)
        {
            return new Dictionary<ReactionType, int>();
        }

        List<Reaction> reactions = session.Interactions
            .OfType<Reaction>()
            .Where(r => r.TurnId == turnId)
            .ToList();

        return reactions
            .GroupBy(r => r.Type)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    // TODO: Add UsePowerUp() - handle power-up activation
    // TODO: Add ValidatePowerUp() - check if power-up can be used
    // TODO: Add GetPlayerInteractionStats() - aggregate player interaction data
    // TODO: Add IdentifyFunnyMoment() - detect humorous interactions
    // TODO: Add RateLimitCheck() - validate interaction frequency
    // TODO: Add BroadcastInteraction() - send to relevant players
    // TODO: Add InteractionReplay() - replay interactions for highlights
}
