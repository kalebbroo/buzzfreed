using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Models.Multiplayer.GameModes;

/// <summary>
/// Hot Seat Mode: Classic turn-based quiz gameplay
///
/// GAMEPLAY:
/// - One player answers at a time (the "hot seat")
/// - Other players spectate and can react
/// - Players rotate through all questions
/// - Individual scoring with leaderboard
///
/// ENGAGEMENT:
/// - Reactions from spectators (limited to 3 per turn)
/// - Live score updates
/// - "Crowd favorite" bonus for most reactions
/// - Streak bonuses for consecutive correct answers
///
/// ROTATION:
/// - Round-robin: P1 → P2 → P3 → P1 → P2 → ...
/// - Each player answers equal number of questions
/// - If questions don't divide evenly, some players get extra
///
/// SCORING:
/// - Correct answer: 100 points
/// - Speed bonus: Up to 50 points (faster = more points)
/// - Crowd favorite: 25 points (if most reactions received)
/// - Streak bonus: 10 points per question in streak
///
/// TODO: Add "pressure mode" - timer decreases each turn
/// TODO: Add "steal" mechanic - spectators can steal question if player times out
/// TODO: Add confidence system - bet points on answer
/// </summary>
public class HotSeatMode : IGameMode
{
    public string ModeId => "hot-seat";
    public string DisplayName => "Hot Seat";
    public string Description => "Take turns in the spotlight! One player answers while others watch and react. Can you handle the pressure?";
    public int MinPlayers => 2;
    public int MaxPlayers => 10;
    public bool RequiresTeams => false;

    public GameModeConfig Config => new GameModeConfig
    {
        DefaultTimeLimit = 30,
        CorrectAnswerPoints = 100,
        IncorrectAnswerPoints = 0,
        EnableSpeedBonus = true,
        MaxSpeedBonus = 50,
        EnableReactions = true,
        EnableSuggestions = false, // Anonymous suggestions not in base mode
        EnablePredictions = false, // Predictions not in base mode
        EnablePowerUps = false
    };

    public void OnGameStart(GameSession session)
    {
        Logs.Info($"Starting Hot Seat game with {session.Players.Count} players");

        // Shuffle player order for fairness
        // TODO: Implement player shuffle
        // session.Players = session.Players.OrderBy(_ => Random.Shared.Next()).ToList();

        // Initialize scores
        foreach (Player player in session.Players)
        {
            session.Scores[player.UserId] = 0;
        }

        // Log game start event
        session.LogEvent(new GameEvent
        {
            Type = GameEventType.GameStarted,
            Data = $"Hot Seat mode with {session.Players.Count} players"
        });

        // TODO: Initialize custom stats (streaks, crowd favorites, etc.)
        // TODO: Show intro animation/instructions to players
    }

    public void OnTurnStart(GameSession session, int questionNumber)
    {
        // Determine whose turn it is (round-robin)
        int playerIndex = (questionNumber - 1) % session.Players.Count;
        string activePlayerId = session.Players[playerIndex].UserId;

        Logs.Debug($"Turn {questionNumber}: Player {activePlayerId} is active");

        // Create turn state
        session.StartTurn(activePlayerId, questionNumber);

        if (session.CurrentTurn != null)
        {
            session.CurrentTurn.Phase = TurnPhase.Question;
        }

        // TODO: Check if player is still connected
        // TODO: If player disconnected, skip to next player
        // TODO: Announce turn start to all players via real-time event
    }

    public bool CanPlayerAnswer(GameSession session, string playerId)
    {
        if (session.CurrentTurn == null)
        {
            Logs.Warning("No active turn, cannot answer");
            return false;
        }

        // Only the active player can answer in Hot Seat mode
        bool isActivePlayer = session.CurrentTurn.ActivePlayerId == playerId;

        if (!isActivePlayer)
        {
            Logs.Debug($"Player {playerId} cannot answer - not their turn");
        }

        // Check if turn is in answering phase
        bool isAnsweringPhase = session.CurrentTurn.Phase == TurnPhase.Question ||
                                session.CurrentTurn.Phase == TurnPhase.Answering;

        // Check if time limit hasn't expired
        bool hasTimeRemaining = (DateTime.UtcNow - session.CurrentTurn.StartTime).TotalSeconds < session.CurrentTurn.TimeLimit;

        return isActivePlayer && isAnsweringPhase && hasTimeRemaining;
    }

    public void OnAnswerSubmit(GameSession session, string playerId, int answerIndex)
    {
        if (session.CurrentTurn == null)
        {
            Logs.Error("Cannot submit answer - no active turn");
            return;
        }

        Logs.Info($"Player {playerId} submitted answer: {answerIndex}");

        // Record response time
        TimeSpan responseTime = DateTime.UtcNow - session.CurrentTurn.StartTime;

        // Create player response
        PlayerResponse response = new PlayerResponse
        {
            PlayerId = playerId,
            AnswerIndex = answerIndex,
            ResponseTime = responseTime,
            Timestamp = DateTime.UtcNow
        };

        // Check if answer is correct
        Question currentQuestion = session.CurrentQuiz.Questions[session.CurrentTurn.QuestionNumber - 1];
        response.IsCorrect = (answerIndex == currentQuestion.CorrectAnswerIndex);

        session.CurrentTurn.Responses.Add(response);

        // Move to reaction phase
        session.CurrentTurn.Phase = TurnPhase.Reaction;

        // Log event
        session.LogEvent(new GameEvent
        {
            Type = GameEventType.PlayerAnswered,
            PlayerId = playerId,
            Data = $"Answer: {answerIndex}, Correct: {response.IsCorrect}, Time: {responseTime.TotalSeconds:F1}s"
        });

        // TODO: Trigger real-time event to show answer to all players
        // TODO: Start reaction timer (allow reactions for 5 seconds)
        // TODO: Update player stats (questions answered, accuracy, avg time, etc.)
        // TODO: Check for streak (consecutive correct answers)
    }

    public Dictionary<string, int> CalculateScore(GameSession session, TurnState turn)
    {
        Dictionary<string, int> scores = new Dictionary<string, int>();

        if (turn.Responses.Count == 0)
        {
            Logs.Warning("No responses in turn, no score to calculate");
            return scores;
        }

        PlayerResponse response = turn.Responses[0]; // Hot Seat only has one answer per turn
        string playerId = response.PlayerId;
        int totalPoints = 0;
        ScoreBreakdown breakdown = new ScoreBreakdown();

        // Base points for correct answer
        if (response.IsCorrect)
        {
            totalPoints += Config.CorrectAnswerPoints;
            breakdown.Add("Correct Answer", Config.CorrectAnswerPoints);

            // Speed bonus (faster = more points)
            if (Config.EnableSpeedBonus)
            {
                int speedBonus = CalculateSpeedBonus(response.ResponseTime, turn.TimeLimit);
                totalPoints += speedBonus;
                breakdown.Add($"Speed Bonus ({response.ResponseTime.TotalSeconds:F1}s)", speedBonus);
            }

            // TODO: Streak bonus
            // int streak = GetPlayerStreak(session, playerId);
            // if (streak > 1)
            // {
            //     int streakBonus = (streak - 1) * 10;
            //     totalPoints += streakBonus;
            //     breakdown.Add($"Streak Bonus (x{streak})", streakBonus);
            // }
        }
        else
        {
            breakdown.Add("Incorrect Answer", 0);
        }

        // Crowd favorite bonus (most reactions)
        if (Config.EnableReactions && turn.Reactions.Count > 0)
        {
            int positiveReactions = turn.Reactions.Count(r =>
                r.Type == ReactionType.Nice ||
                r.Type == ReactionType.Smart ||
                r.Type == ReactionType.Fire);

            if (positiveReactions >= 3) // At least 3 positive reactions
            {
                int crowdBonus = 25;
                totalPoints += crowdBonus;
                breakdown.Add($"Crowd Favorite ({positiveReactions} reactions)", crowdBonus);
            }
        }

        scores[playerId] = totalPoints;

        // Store breakdown for results screen
        Player? player = session.Players.FirstOrDefault(p => p.UserId == playerId);
        if (player != null)
        {
            player.ScoreBreakdown = breakdown;
        }

        Logs.Info($"Turn score for {playerId}: {totalPoints} points");
        return scores;
    }

    public void OnTurnEnd(GameSession session)
    {
        if (session.CurrentTurn == null) return;

        // Calculate scores
        Dictionary<string, int> turnScores = CalculateScore(session, session.CurrentTurn);

        // Update session scores
        foreach (KeyValuePair<string, int> score in turnScores)
        {
            if (session.Scores.ContainsKey(score.Key))
            {
                session.Scores[score.Key] += score.Value;
            }
            else
            {
                session.Scores[score.Key] = score.Value;
            }

            // Add to player score
            Player? player = session.Players.FirstOrDefault(p => p.UserId == score.Key);
            if (player != null)
            {
                player.AddScore(score.Value, $"Question {session.CurrentTurn.QuestionNumber}");
            }
        }

        // Mark turn as complete
        session.CurrentTurn.Phase = TurnPhase.Complete;
        session.EndTurn();

        // Update session stats
        session.Stats.TotalAnswers++;
        session.Stats.TotalReactions += session.CurrentTurn.Reactions.Count;

        // Check for leader change
        string? currentLeader = session.Scores.OrderByDescending(s => s.Value).FirstOrDefault().Key;
        // TODO: Track previous leader and log LeaderChanged event if different

        Logs.Info($"Turn {session.CurrentTurn.QuestionNumber} complete");

        // TODO: Process reactions and update reaction stats
        // TODO: Check for highlights (funny moment, clutch play, etc.)
        // TODO: Wait for results display time before starting next turn
    }

    public string GetNextActivePlayer(GameSession session)
    {
        if (session.CurrentTurn == null)
        {
            // First turn, start with first player
            return session.Players.First().UserId;
        }

        // Round-robin rotation
        int currentIndex = session.Players.FindIndex(p => p.UserId == session.CurrentTurn.ActivePlayerId);
        int nextIndex = (currentIndex + 1) % session.Players.Count;

        return session.Players[nextIndex].UserId;
    }

    public bool IsGameComplete(GameSession session)
    {
        // Game is complete when all questions have been answered
        int totalQuestions = session.CurrentQuiz.Questions.Count;
        int questionsAnswered = session.Stats.TotalAnswers;

        bool isComplete = questionsAnswered >= totalQuestions;

        if (isComplete)
        {
            Logs.Info($"Game complete: {questionsAnswered}/{totalQuestions} questions answered");
        }

        return isComplete;
    }

    public void OnGameEnd(GameSession session)
    {
        Logs.Info($"Ending Hot Seat game session {session.SessionId}");

        session.State = SessionState.Calculating;

        // Calculate final scores (already done during turns)
        List<KeyValuePair<string, int>> leaderboard = session.Scores
            .OrderByDescending(s => s.Value)
            .ToList();

        // Determine winner
        if (leaderboard.Count > 0)
        {
            string winnerId = leaderboard[0].Key;
            int winningScore = leaderboard[0].Value;

            session.LogEvent(new GameEvent
            {
                Type = GameEventType.GameEnded,
                PlayerId = winnerId,
                Data = $"Winner: {winnerId} with {winningScore} points",
                IsHighlight = true,
                Category = HighlightCategory.Impressive
            });
        }

        // Calculate session stats
        session.Stats.EndTime = DateTime.UtcNow;
        session.Stats.TotalQuestions = session.CurrentQuiz.Questions.Count;

        // Find fastest player
        // TODO: Calculate from turn responses
        // session.Stats.FastestPlayer = GetFastestPlayer(session);

        // Find most accurate player
        // TODO: Calculate accuracy percentage
        // session.Stats.MostAccuratePlayer = GetMostAccuratePlayer(session);

        // Find most reactions received
        // TODO: Aggregate reactions per player
        // session.Stats.MostReactionsPlayer = GetMostReactedPlayer(session);

        session.State = SessionState.Completed;
        session.EndedAt = DateTime.UtcNow;

        // TODO: Generate AI summary of game
        // TODO: Identify highlight moments
        // TODO: Calculate achievements earned
        // TODO: Save session to database
    }

    public GameModeUIConfig GetUIConfig()
    {
        return new GameModeUIConfig
        {
            ShowTimer = true,
            ShowLiveScores = true,
            ShowOtherAnswers = false, // Don't show until after answer
            ShowReactionWidget = true,
            ShowSuggestionWidget = false,
            ShowPredictionWidget = false,
            ShowPowerUps = false,
            CustomUIElements = new List<string>
            {
                "hot-seat-spotlight", // Highlight active player
                "reaction-burst-animation", // Animate reactions
                "streak-indicator" // Show current streak
            }
        };
    }

    // Helper methods

    /// <summary>
    /// Calculate speed bonus based on response time
    /// Faster responses get more points
    /// </summary>
    private int CalculateSpeedBonus(TimeSpan responseTime, int timeLimit)
    {
        double secondsRemaining = timeLimit - responseTime.TotalSeconds;

        if (secondsRemaining <= 0)
        {
            return 0; // No bonus if time ran out
        }

        // Linear scale: full time = 0 bonus, instant = max bonus
        double bonusPercentage = secondsRemaining / timeLimit;
        int speedBonus = (int)(Config.MaxSpeedBonus * bonusPercentage);

        return Math.Max(0, speedBonus);
    }

    // TODO: Add GetPlayerStreak() - count consecutive correct answers
    // TODO: Add GetFastestPlayer() - find player with lowest average time
    // TODO: Add GetMostAccuratePlayer() - find player with highest accuracy
    // TODO: Add GetMostReactedPlayer() - find player who received most reactions
    // TODO: Add IdentifyHighlights() - find funny/dramatic moments
}
