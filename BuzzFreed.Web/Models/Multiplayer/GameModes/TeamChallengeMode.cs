using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Models.Multiplayer.GameModes;

/// <summary>
/// Team Challenge Mode: Cooperative team-based quiz gameplay
///
/// GAMEPLAY:
/// - Players divided into 2-4 teams
/// - Teams vote on answers collaboratively
/// - Captain can override team vote (optional)
/// - Team with highest score wins
///
/// VOTING SYSTEM:
/// - Each team member casts vote privately
/// - Majority answer is submitted
/// - If tie, captain breaks tie (or random if no captain)
/// - Votes can be changed until time expires or captain locks
///
/// ENGAGEMENT:
/// - Team chat for discussion
/// - Vote progress indicator (X/Y votes in)
/// - Consensus strength bonus (unanimous = more points)
/// - Captain pressure (override responsibility)
///
/// ROTATION:
/// - Captain rotates each turn for fairness
/// - All teams answer the same question simultaneously
/// - Scoring based on speed + correctness + consensus
///
/// SCORING:
/// - Correct answer: 100 points (team)
/// - Speed bonus: Up to 50 points (first team to submit)
/// - Consensus bonus: +25 points if unanimous vote
/// - Captain bonus: +10 points if captain's vote was correct
///
/// TODO: Implement voting timeout (e.g., 5 seconds before force submit)
/// TODO: Add "split" mechanic - team can split and both answers submitted
/// TODO: Add team power-ups (extra time, steal points, etc.)
/// TODO: Implement captain veto count limit (can't override every time)
/// </summary>
public class TeamChallengeMode : IGameMode
{
    public string ModeId => "team-challenge";
    public string DisplayName => "Team Challenge";
    public string Description => "Work together! Teams vote on answers and compete for the highest score. Communication is key!";
    public int MinPlayers => 4; // Minimum 2 teams of 2
    public int MaxPlayers => 12; // Maximum 4 teams of 3
    public bool RequiresTeams => true;

    public GameModeConfig Config => new GameModeConfig
    {
        DefaultTimeLimit = 45, // More time for discussion
        CorrectAnswerPoints = 100,
        IncorrectAnswerPoints = 0,
        EnableSpeedBonus = true,
        MaxSpeedBonus = 50,
        EnableReactions = true,
        EnableSuggestions = false,
        EnablePredictions = false,
        EnablePowerUps = false,
        CustomSettings = new Dictionary<string, object>
        {
            { "AllowCaptainOverride", true },
            { "ConsensusBonus", 25 },
            { "FirstTeamBonus", 20 }
        }
    };

    public void OnGameStart(GameSession session)
    {
        Logs.Info($"Starting Team Challenge game with {session.Teams?.Count ?? 0} teams");

        if (session.Teams == null || session.Teams.Count < 2)
        {
            Logs.Error("Team Challenge requires at least 2 teams");
            // TODO: Throw exception or handle gracefully
            return;
        }

        // Initialize team scores
        foreach (Team team in session.Teams.Values)
        {
            session.Scores[team.TeamId] = 0;

            // Assign first captain (first player in team)
            if (team.PlayerIds.Count > 0)
            {
                team.CaptainId = team.PlayerIds[0];
            }
        }

        // Log game start
        session.LogEvent(new GameEvent
        {
            Type = GameEventType.GameStarted,
            Data = $"Team Challenge with {session.Teams.Count} teams"
        });

        // TODO: Show team rosters to all players
        // TODO: Explain voting mechanics
        // TODO: Initialize team chat channels
    }

    public void OnTurnStart(GameSession session, int questionNumber)
    {
        Logs.Debug($"Turn {questionNumber} starting for all teams");

        // In Team Challenge, all teams answer simultaneously
        // Use first team's ID as "active" for turn tracking purposes
        string? firstTeamId = session.Teams?.Values.FirstOrDefault()?.TeamId;

        if (firstTeamId == null)
        {
            Logs.Error("No teams found");
            return;
        }

        session.StartTurn(firstTeamId, questionNumber);

        if (session.CurrentTurn == null) return;

        session.CurrentTurn.Phase = TurnPhase.Question;

        // Initialize voting for each team
        if (session.Teams != null)
        {
            foreach (Team team in session.Teams.Values)
            {
                team.CurrentVote = new TeamVote
                {
                    Votes = new Dictionary<string, int>(),
                    IsLocked = false,
                    FinalAnswer = null
                };
            }
        }

        // Rotate captains for next turn
        if (session.Teams != null)
        {
            foreach (Team team in session.Teams.Values)
            {
                team.RotateCaptain();
            }
        }

        // Log event
        session.LogEvent(new GameEvent
        {
            Type = GameEventType.TeamVoteStarted,
            Data = $"Question {questionNumber}"
        });

        // TODO: Broadcast question to all teams
        // TODO: Start team chat voting phase
        // TODO: Show vote progress indicators
    }

    public bool CanPlayerAnswer(GameSession session, string playerId)
    {
        if (session.CurrentTurn == null) return false;

        // In Team Challenge, "answering" means casting a vote
        // Any team member can vote if:
        // 1. Turn is in answering phase
        // 2. Their team hasn't locked in answer
        // 3. Time hasn't expired

        // Find player's team
        Team? playerTeam = session.Teams?.Values.FirstOrDefault(t => t.PlayerIds.Contains(playerId));

        if (playerTeam == null)
        {
            Logs.Warning($"Player {playerId} not found in any team");
            return false;
        }

        // Check if team vote is locked
        if (playerTeam.CurrentVote?.IsLocked == true)
        {
            return false;
        }

        // Check phase
        bool isVotingPhase = session.CurrentTurn.Phase == TurnPhase.Question ||
                            session.CurrentTurn.Phase == TurnPhase.Answering;

        // Check time
        bool hasTimeRemaining = (DateTime.UtcNow - session.CurrentTurn.StartTime).TotalSeconds < session.CurrentTurn.TimeLimit;

        return isVotingPhase && hasTimeRemaining;
    }

    public void OnAnswerSubmit(GameSession session, string playerId, int answerIndex)
    {
        if (session.CurrentTurn == null || session.Teams == null)
        {
            Logs.Error("Cannot submit vote - no active turn or teams");
            return;
        }

        // Find player's team
        Team? playerTeam = session.Teams.Values.FirstOrDefault(t => t.PlayerIds.Contains(playerId));

        if (playerTeam == null || playerTeam.CurrentVote == null)
        {
            Logs.Error($"Player {playerId} not found in team or no active vote");
            return;
        }

        // Record vote
        int? previousVote = playerTeam.CurrentVote.Votes.ContainsKey(playerId)
            ? playerTeam.CurrentVote.Votes[playerId]
            : null;

        playerTeam.CurrentVote.Votes[playerId] = answerIndex;

        Logs.Info($"Player {playerId} voted for answer {answerIndex} (Team: {playerTeam.Name})");

        // Log vote interaction
        session.Interactions.Add(new TeamVoteInteraction
        {
            SessionId = session.SessionId,
            TurnId = session.CurrentTurn.TurnId,
            PlayerId = playerId,
            TeamId = playerTeam.TeamId,
            QuestionNumber = session.CurrentTurn.QuestionNumber,
            VotedAnswerIndex = answerIndex,
            PreviousVote = previousVote,
            CanChange = !playerTeam.CurrentVote.IsLocked
        });

        // Check if all team members have voted
        bool allVotedIn = playerTeam.CurrentVote.Votes.Count == playerTeam.PlayerIds.Count;

        if (allVotedIn)
        {
            // Auto-lock if all votes are in and unanimous
            int? majority = playerTeam.CurrentVote.GetMajority();
            bool isUnanimous = playerTeam.CurrentVote.Votes.Values.Distinct().Count() == 1;

            if (isUnanimous)
            {
                LockTeamAnswer(session, playerTeam, majority!.Value);
            }
        }

        // TODO: Broadcast vote progress to team (X/Y votes in)
        // TODO: Show vote distribution to captain only
        // TODO: Allow vote changes until lock
    }

    public Dictionary<string, int> CalculateScore(GameSession session, TurnState turn)
    {
        Dictionary<string, int> scores = new Dictionary<string, int>();

        if (session.Teams == null)
        {
            Logs.Error("No teams found for scoring");
            return scores;
        }

        Question currentQuestion = session.CurrentQuiz.Questions[turn.QuestionNumber - 1];
        List<Team> teamsAnswered = new List<Team>();

        // Collect all team responses
        foreach (Team team in session.Teams.Values)
        {
            if (team.CurrentVote?.FinalAnswer != null)
            {
                teamsAnswered.Add(team);
            }
        }

        // Sort by submission time (for speed bonus)
        // TODO: Track actual submission timestamps
        teamsAnswered = teamsAnswered.OrderBy(t => t.CurrentVote?.Votes.Count).ToList();

        bool firstCorrectAwarded = false;

        foreach (Team team in teamsAnswered)
        {
            int totalPoints = 0;
            ScoreBreakdown breakdown = new ScoreBreakdown();

            bool isCorrect = team.CurrentVote?.FinalAnswer == currentQuestion.CorrectAnswerIndex;

            if (isCorrect)
            {
                // Base points
                totalPoints += Config.CorrectAnswerPoints;
                breakdown.Add("Correct Answer", Config.CorrectAnswerPoints);

                // First team bonus
                if (!firstCorrectAwarded)
                {
                    int firstBonus = (int)(Config.CustomSettings["FirstTeamBonus"]);
                    totalPoints += firstBonus;
                    breakdown.Add("First Team", firstBonus);
                    firstCorrectAwarded = true;
                }

                // Consensus bonus (unanimous vote)
                if (team.CurrentVote != null)
                {
                    double consensusStrength = team.CurrentVote.GetConsensusStrength();
                    if (consensusStrength >= 1.0) // 100% agreement
                    {
                        int consensusBonus = (int)(Config.CustomSettings["ConsensusBonus"]);
                        totalPoints += consensusBonus;
                        breakdown.Add("Unanimous Vote", consensusBonus);
                    }
                }

                // TODO: Captain bonus if captain's vote was correct
                // TODO: Speed bonus based on submission time
            }
            else
            {
                breakdown.Add("Incorrect Answer", 0);
            }

            scores[team.TeamId] = totalPoints;
            team.ScoreBreakdown.Add($"Q{turn.QuestionNumber}", totalPoints, breakdown.ToString());
        }

        return scores;
    }

    public void OnTurnEnd(GameSession session)
    {
        if (session.CurrentTurn == null || session.Teams == null) return;

        // Calculate and apply scores
        Dictionary<string, int> turnScores = CalculateScore(session, session.CurrentTurn);

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

            // Update team score
            if (session.Teams.TryGetValue(score.Key, out Team? team))
            {
                team.Score += score.Value;
            }
        }

        // Mark turn complete
        session.CurrentTurn.Phase = TurnPhase.Complete;
        session.EndTurn();

        // Clear team votes
        foreach (Team team in session.Teams.Values)
        {
            team.CurrentVote = null;
        }

        Logs.Info($"Team Challenge turn {session.CurrentTurn.QuestionNumber} complete");

        // TODO: Show team vote distributions in results
        // TODO: Highlight unanimous decisions
        // TODO: Show captain overrides
    }

    public string GetNextActivePlayer(GameSession session)
    {
        // Team Challenge doesn't have a single active player
        // Return first team ID for tracking purposes
        return session.Teams?.Values.FirstOrDefault()?.TeamId ?? string.Empty;
    }

    public bool IsGameComplete(GameSession session)
    {
        int totalQuestions = session.CurrentQuiz.Questions.Count;
        int questionsAnswered = session.Stats.TotalAnswers;

        // In Team Challenge, one "answer" per question (all teams vote together)
        return questionsAnswered >= totalQuestions;
    }

    public void OnGameEnd(GameSession session)
    {
        if (session.Teams == null) return;

        Logs.Info($"Ending Team Challenge game session {session.SessionId}");

        session.State = SessionState.Calculating;

        // Calculate final team standings
        List<Team> leaderboard = session.Teams.Values
            .OrderByDescending(t => t.Score)
            .ToList();

        if (leaderboard.Count > 0)
        {
            Team winningTeam = leaderboard[0];

            session.LogEvent(new GameEvent
            {
                Type = GameEventType.GameEnded,
                TeamId = winningTeam.TeamId,
                Data = $"Winning Team: {winningTeam.Name} with {winningTeam.Score} points",
                IsHighlight = true,
                Category = HighlightCategory.Impressive
            });
        }

        session.State = SessionState.Completed;
        session.EndedAt = DateTime.UtcNow;

        // TODO: Calculate team stats (avg consensus, captain accuracy, etc.)
        // TODO: Find MVP (most valuable player) per team
        // TODO: Identify best team moment
        // TODO: Save session to database
    }

    public GameModeUIConfig GetUIConfig()
    {
        return new GameModeUIConfig
        {
            ShowTimer = true,
            ShowLiveScores = true,
            ShowOtherAnswers = false,
            ShowReactionWidget = true,
            ShowSuggestionWidget = false,
            ShowPredictionWidget = false,
            ShowPowerUps = false,
            CustomUIElements = new List<string>
            {
                "team-vote-widget", // Show vote progress
                "team-chat", // Team discussion
                "captain-indicator", // Highlight current captain
                "consensus-meter" // Show vote distribution
            }
        };
    }

    // Helper methods

    /// <summary>
    /// Lock in team's final answer
    /// Can be called by captain override or auto-lock
    /// </summary>
    private void LockTeamAnswer(GameSession session, Team team, int answerIndex)
    {
        if (team.CurrentVote == null) return;

        team.CurrentVote.IsLocked = true;
        team.CurrentVote.FinalAnswer = answerIndex;

        // Record team response
        if (session.CurrentTurn != null)
        {
            session.CurrentTurn.Responses.Add(new PlayerResponse
            {
                PlayerId = team.TeamId, // Use team ID as "player" ID
                AnswerIndex = answerIndex,
                ResponseTime = DateTime.UtcNow - session.CurrentTurn.StartTime,
                Timestamp = DateTime.UtcNow
            });

            // Log event
            session.LogEvent(new GameEvent
            {
                Type = GameEventType.TeamAnswered,
                TeamId = team.TeamId,
                Data = $"Answer: {answerIndex}"
            });
        }

        Logs.Info($"Team {team.Name} locked answer: {answerIndex}");

        // TODO: Broadcast lock to team members
        // TODO: Check if all teams have locked in
        // TODO: If all locked, end turn early (no need to wait for timer)
    }

    // TODO: Add HandleCaptainOverride() - captain can force answer
    // TODO: Add CalculateTeamMVP() - most accurate voter
    // TODO: Add GetBestTeamMoment() - highlight close votes
    // TODO: Add CalculateCaptainAccuracy() - how often captain was right
}
