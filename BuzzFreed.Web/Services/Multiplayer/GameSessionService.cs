using System.Collections.Concurrent;
using System.Text.Json;
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.Models;
using BuzzFreed.Web.Models.Multiplayer;
using BuzzFreed.Web.Models.Multiplayer.GameModes;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Services.Multiplayer;

/// <summary>
/// Service for managing active game sessions
///
/// RESPONSIBILITIES:
/// - Create sessions from rooms
/// - Generate quizzes via AI
/// - Manage turn progression
/// - Process player answers
/// - Calculate scores
/// - Handle game completion
/// - Session persistence
///
/// GAME FLOW:
/// 1. CreateSession() - Initialize from GameRoom
/// 2. GenerateQuiz() - Call AI to create questions
/// 3. StartSession() - Begin first turn
/// 4. NextTurn() - Progress through questions
/// 5. ProcessAnswer() - Handle submissions
/// 6. EndSession() - Calculate final results
///
/// STATE MANAGEMENT:
/// - Sessions stored in memory (active games)
/// - Turn state tracked per session
/// - Real-time events broadcast to players
/// - Session history saved to database
///
/// DELEGATION:
/// - Game mode logic delegated to IGameMode implementations
/// - AI quiz generation delegated to AIProviderRegistry
/// - Real-time events delegated to EventService
///
/// TODO: Add pause/resume functionality
/// TODO: Add reconnection handling (player disconnect)
/// TODO: Add session recording for replays
/// TODO: Add turn time extension (vote for more time)
/// </summary>
public class GameSessionService(
    GameModeRegistry gameModeRegistry,
    AIProviderRegistry aiProviderRegistry)
{
    public readonly GameModeRegistry GameModeRegistry = gameModeRegistry;
    public readonly AIProviderRegistry AIProviderRegistry = aiProviderRegistry;
    public readonly ConcurrentDictionary<string, GameSession> ActiveSessions = new();

    /// <summary>
    /// Create a new game session from a room
    /// </summary>
    /// <param name="room">Room to convert to session</param>
    /// <returns>Created session</returns>
    public async Task<GameSession> CreateSessionAsync(GameRoom room)
    {
        Logs.Info($"Creating game session from room {room.RoomCode}");

        GameSession session = new GameSession
        {
            SessionId = Guid.NewGuid().ToString(),
            RoomId = room.RoomId,
            GuildId = room.GuildId,
            GameMode = room.GameMode,
            Settings = room.QuizSettings,
            State = SessionState.Starting,
            Players = room.Players.ToList(),
            Teams = room.Teams,
            CreatedAt = DateTime.UtcNow
        };

        // Initialize scores
        if (session.Teams != null)
        {
            // Team-based mode
            foreach (Team team in session.Teams.Values)
            {
                session.Scores[team.TeamId] = 0;
            }
        }
        else
        {
            // Individual mode
            foreach (Player player in session.Players)
            {
                session.Scores[player.UserId] = 0;
            }
        }

        // Store session
        ActiveSessions[session.SessionId] = session;

        // Generate quiz
        await GenerateQuizAsync(session);

        Logs.Info($"Session created: {session.SessionId}");

        // TODO: Broadcast session created event
        // TODO: Schedule session timeout handler

        return session;
    }

    /// <summary>
    /// Generate quiz using AI based on settings
    /// </summary>
    private async Task GenerateQuizAsync(GameSession session)
    {
        Logs.Info($"Generating quiz for session {session.SessionId}");

        try
        {
            // Get AI provider
            ILLMProvider? provider = await AIProviderRegistry.GetLLMProviderAsync();

            if (provider == null)
            {
                Logs.Error("No AI provider available for quiz generation");
                Logs.Warning("Falling back to placeholder quiz");
                session.CurrentQuiz = CreatePlaceholderQuiz(session.Settings);
                session.State = SessionState.Active;
                session.StartedAt = DateTime.UtcNow;
                return;
            }

            // Build prompt from settings
            string prompt = BuildQuizPrompt(session.Settings);

            Logs.Debug($"Calling AI provider to generate quiz with prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");

            // Call AI provider to generate quiz
            LLMResponse response = await provider.GenerateCompletionAsync(new LLMRequest
            {
                Prompt = prompt,
                SystemMessage = @"You are a creative quiz generator for a multiplayer trivia game.
Generate engaging, fun, and challenging questions that players will enjoy.
Always respond with valid JSON only - no markdown formatting, no code blocks, no additional text.
Make questions entertaining and social - perfect for friends playing together.",
                MaxTokens = 3000,
                Temperature = 0.8,
                Model = null // Use default model
            });

            if (!response.IsSuccess || string.IsNullOrEmpty(response.Text))
            {
                Logs.Error($"AI generation failed: {response.Error ?? "Empty response"}");
                Logs.Warning("Falling back to placeholder quiz");
                session.CurrentQuiz = CreatePlaceholderQuiz(session.Settings);
            }
            else
            {
                // Parse response into Quiz object
                try
                {
                    session.CurrentQuiz = ParseQuizResponse(response.Text, session.Settings);
                    Logs.Info($"Quiz generated successfully by {response.Provider}/{response.Model}");
                }
                catch (Exception parseEx)
                {
                    Logs.Error($"Failed to parse AI response: {parseEx.Message}");
                    Logs.Warning("Falling back to placeholder quiz");
                    session.CurrentQuiz = CreatePlaceholderQuiz(session.Settings);
                }
            }

            session.State = SessionState.Active;
            session.StartedAt = DateTime.UtcNow;

            Logs.Info($"Quiz ready: {session.CurrentQuiz.Questions.Count} questions, Title: {session.CurrentQuiz.Title}");
        }
        catch (Exception ex)
        {
            Logs.Error($"Error generating quiz: {ex.Message}");
            Logs.Error($"Stack trace: {ex.StackTrace}");
            session.State = SessionState.Aborted;
            // TODO: Broadcast error to players
        }
    }

    /// <summary>
    /// Start the game session (begin first turn)
    /// </summary>
    public bool StartSession(string sessionId)
    {
        if (!ActiveSessions.TryGetValue(sessionId, out GameSession? session))
        {
            Logs.Warning($"Session not found: {sessionId}");
            return false;
        }

        if (session.State != SessionState.Active)
        {
            Logs.Warning($"Session {sessionId} not in active state");
            return false;
        }

        // Get game mode
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode == null)
        {
            Logs.Error($"Game mode not found: {session.GameMode}");
            return false;
        }

        // Initialize game
        mode.OnGameStart(session);

        // Start first turn
        return StartNextTurn(sessionId);
    }

    /// <summary>
    /// Start the next turn
    /// </summary>
    public bool StartNextTurn(string sessionId)
    {
        if (!ActiveSessions.TryGetValue(sessionId, out GameSession? session))
        {
            return false;
        }

        // Get game mode
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode == null)
        {
            return false;
        }

        // Check if game is complete
        if (mode.IsGameComplete(session))
        {
            EndSession(sessionId);
            return false;
        }

        // Determine next question number
        int nextQuestionNumber = session.Stats.TotalAnswers + 1;

        // Let mode start the turn
        mode.OnTurnStart(session, nextQuestionNumber);

        Logs.Info($"Session {sessionId}: Turn {nextQuestionNumber} started");

        // TODO: Broadcast turn start event to all players
        // TODO: Start turn timer
        // TODO: Enable answer submission UI

        return true;
    }

    /// <summary>
    /// Process a player's answer submission
    /// </summary>
    public bool SubmitAnswer(string sessionId, string playerId, int answerIndex)
    {
        if (!ActiveSessions.TryGetValue(sessionId, out GameSession? session))
        {
            Logs.Warning($"Session not found: {sessionId}");
            return false;
        }

        // Get game mode
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode == null)
        {
            Logs.Error($"Game mode not found: {session.GameMode}");
            return false;
        }

        // Validate player can answer
        if (!mode.CanPlayerAnswer(session, playerId))
        {
            Logs.Warning($"Player {playerId} cannot answer in current state");
            return false;
        }

        // Let mode process answer
        mode.OnAnswerSubmit(session, playerId, answerIndex);

        // TODO: Check if turn should end (all players answered, time expired, etc.)
        // TODO: Broadcast answer submission event

        return true;
    }

    /// <summary>
    /// End current turn and move to next
    /// </summary>
    public bool EndCurrentTurn(string sessionId)
    {
        if (!ActiveSessions.TryGetValue(sessionId, out GameSession? session))
        {
            return false;
        }

        if (session.CurrentTurn == null)
        {
            Logs.Warning($"No active turn in session {sessionId}");
            return false;
        }

        // Get game mode
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode == null)
        {
            return false;
        }

        // Let mode handle turn end
        mode.OnTurnEnd(session);

        Logs.Info($"Session {sessionId}: Turn {session.CurrentTurn.QuestionNumber} ended");

        // TODO: Broadcast turn end event
        // TODO: Show results screen for X seconds
        // TODO: Then automatically start next turn

        return true;
    }

    /// <summary>
    /// End the game session
    /// </summary>
    public bool EndSession(string sessionId)
    {
        if (!ActiveSessions.TryGetValue(sessionId, out GameSession? session))
        {
            return false;
        }

        Logs.Info($"Ending session {sessionId}");

        // Get game mode
        IGameMode? mode = GameModeRegistry.GetMode(session.GameMode);
        if (mode != null)
        {
            mode.OnGameEnd(session);
        }

        session.State = SessionState.Completed;
        session.EndedAt = DateTime.UtcNow;

        // TODO: Calculate final statistics
        // TODO: Generate AI summary
        // TODO: Identify highlights
        // TODO: Save session to database
        // TODO: Broadcast game end event
        // TODO: Show final results screen

        // Remove from active sessions after delay
        // TODO: Schedule cleanup task

        return true;
    }

    /// <summary>
    /// Get session by ID
    /// </summary>
    public GameSession? GetSession(string sessionId)
    {
        ActiveSessions.TryGetValue(sessionId, out GameSession? session);
        return session;
    }

    /// <summary>
    /// Get all active sessions
    /// </summary>
    public List<GameSession> GetActiveSessions()
    {
        return ActiveSessions.Values.ToList();
    }

    /// <summary>
    /// Abort a session (error, host left, etc.)
    /// </summary>
    public bool AbortSession(string sessionId, string reason)
    {
        if (!ActiveSessions.TryGetValue(sessionId, out GameSession? session))
        {
            return false;
        }

        Logs.Warning($"Aborting session {sessionId}: {reason}");

        session.State = SessionState.Aborted;
        session.EndedAt = DateTime.UtcNow;

        session.LogEvent(new GameEvent
        {
            Type = GameEventType.GameEnded,
            Data = $"Aborted: {reason}"
        });

        // TODO: Broadcast abort event to players
        // TODO: Save partial session data
        // TODO: Cleanup resources

        ActiveSessions.TryRemove(sessionId, out _);

        return true;
    }

    // Helper methods

    /// <summary>
    /// Build AI prompt from quiz customization settings
    /// </summary>
    private string BuildQuizPrompt(QuizCustomization settings)
    {
        // Build comprehensive prompt based on all settings
        var promptParts = new List<string>();

        // Base instruction
        promptParts.Add("Generate a multiplayer trivia quiz in JSON format.");

        // Topic and category
        if (!string.IsNullOrEmpty(settings.CustomPrompt))
        {
            promptParts.Add($"Theme: {settings.CustomPrompt}");
        }
        else if (!string.IsNullOrEmpty(settings.Topic))
        {
            promptParts.Add($"Topic: {settings.Topic}");
        }
        else if (!string.IsNullOrEmpty(settings.Category))
        {
            promptParts.Add($"Category: {settings.Category}");
        }
        else
        {
            promptParts.Add("Topic: General knowledge with a fun, engaging mix of pop culture and trivia");
        }

        // Style
        string styleDescription = settings.Style switch
        {
            QuestionStyle.Deep => "Ask thought-provoking, philosophical questions that make players think deeply.",
            QuestionStyle.Chaotic => "Create wild, unpredictable, absurd questions that surprise players.",
            QuestionStyle.Rapid => "Generate quick-fire, straightforward questions for fast gameplay.",
            QuestionStyle.Story => "Craft narrative-driven questions that build on each other with storylines.",
            _ => "Create classic trivia questions in the style of BuzzFeed quizzes - fun, engaging, and social."
        };
        promptParts.Add(styleDescription);

        // Difficulty
        string difficultyDescription = settings.Difficulty switch
        {
            Difficulty.Challenging => "Make questions challenging that require real knowledge. Mix well-known facts with deeper cuts.",
            Difficulty.Absurd => "Make questions extremely difficult, niche, or absurdly specific. Only experts should know these!",
            _ => "Keep questions accessible and fun. Players should feel smart, not frustrated."
        };
        promptParts.Add(difficultyDescription);

        // Question count
        promptParts.Add($"Generate exactly {settings.QuestionCount} questions.");

        // Answer options
        promptParts.Add($"Each question must have exactly {settings.AnswerCount} answer options.");

        // Explanations
        if (settings.IncludeExplanations)
        {
            promptParts.Add("Include a brief, interesting explanation for each correct answer.");
        }

        // Format requirements
        promptParts.Add("\nIMPORTANT - Response Format:");
        promptParts.Add("Return ONLY valid JSON in this exact structure (no markdown, no code blocks, no additional text):");
        promptParts.Add(@"{
  ""title"": ""Quiz Title"",
  ""description"": ""Brief quiz description"",
  ""questions"": [
    {
      ""text"": ""Question text here?"",
      ""answers"": [""Option A"", ""Option B"", ""Option C"", ""Option D""],
      ""correctIndex"": 0,
      ""explanation"": ""Why this is the correct answer"",
      ""category"": ""Category tag""
    }
  ]
}");

        return string.Join(" ", promptParts);
    }

    /// <summary>
    /// Parse AI response into Quiz object
    /// </summary>
    private Quiz ParseQuizResponse(string responseText, QuizCustomization settings)
    {
        try
        {
            // Clean the response (remove markdown code blocks if present)
            string cleanedJson = CleanJsonResponse(responseText);

            // Parse JSON with options for case-insensitive property names
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true
            };

            var quizData = JsonSerializer.Deserialize<QuizJsonResponse>(cleanedJson, options);

            if (quizData == null || quizData.Questions == null || quizData.Questions.Count == 0)
            {
                Logs.Error("Failed to parse quiz JSON - null or empty questions");
                throw new Exception("Invalid quiz data received from AI");
            }

            // Convert to Quiz model
            Quiz quiz = new Quiz
            {
                QuizId = Guid.NewGuid().ToString(),
                Title = quizData.Title ?? $"Quiz: {settings.Topic ?? "General"}",
                Topic = settings.Topic ?? settings.Category ?? "General",
                Description = quizData.Description,
                CreatedBy = "AI",
                CreatedAt = DateTime.UtcNow,
                Questions = new List<Question>()
            };

            // Convert questions
            foreach (var q in quizData.Questions)
            {
                if (string.IsNullOrEmpty(q.Text) || q.Answers == null || q.Answers.Count == 0)
                {
                    Logs.Warning("Skipping invalid question in AI response");
                    continue;
                }

                quiz.Questions.Add(new Question
                {
                    QuestionId = Guid.NewGuid().ToString(),
                    Text = q.Text,
                    Answers = q.Answers,
                    CorrectAnswerIndex = q.CorrectIndex,
                    Explanation = q.Explanation,
                    Category = q.Category,
                    ImageUrl = q.ImageUrl,
                    Difficulty = settings.Difficulty.ToString()
                });
            }

            Logs.Info($"Successfully parsed {quiz.Questions.Count} questions from AI response");
            return quiz;
        }
        catch (JsonException ex)
        {
            Logs.Error($"JSON parsing error: {ex.Message}");
            Logs.Debug($"Response text: {responseText}");
            throw new Exception($"Failed to parse quiz JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Clean JSON response by removing markdown code blocks and extra whitespace
    /// </summary>
    private string CleanJsonResponse(string response)
    {
        // Remove markdown code blocks (```json ... ``` or ``` ... ```)
        response = response.Trim();

        if (response.StartsWith("```"))
        {
            // Find first newline after ```
            int start = response.IndexOf('\n');
            if (start >= 0)
            {
                response = response.Substring(start + 1);
            }

            // Remove closing ```
            int end = response.LastIndexOf("```");
            if (end >= 0)
            {
                response = response.Substring(0, end);
            }
        }

        return response.Trim();
    }

    /// <summary>
    /// DTO for parsing quiz JSON from AI
    /// </summary>
    private class QuizJsonResponse
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<QuestionJsonResponse> Questions { get; set; } = new();
    }

    /// <summary>
    /// DTO for parsing question JSON from AI
    /// </summary>
    private class QuestionJsonResponse
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Answers { get; set; } = new();
        public int CorrectIndex { get; set; }
        public string? Explanation { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
    }

    /// <summary>
    /// Create a placeholder quiz for testing
    /// </summary>
    private Quiz CreatePlaceholderQuiz(QuizCustomization settings)
    {
        Quiz quiz = new Quiz
        {
            QuizId = Guid.NewGuid().ToString(),
            Title = $"Sample Quiz: {settings.Topic ?? "General"}",
            Questions = new List<Question>()
        };

        int questionCount = settings.QuestionCount > 0 ? settings.QuestionCount : 10;

        for (int i = 0; i < questionCount; i++)
        {
            quiz.Questions.Add(new Question
            {
                QuestionId = Guid.NewGuid().ToString(),
                Text = $"Sample Question {i + 1}",
                Answers = new List<string>
                {
                    "Answer A",
                    "Answer B",
                    "Answer C",
                    "Answer D"
                },
                CorrectAnswerIndex = i % 4, // Rotate correct answer
                ImageUrl = null
            });
        }

        return quiz;
    }

    // TODO: Add PauseSession() - pause game (vote or host action)
    // TODO: Add ResumeSession() - unpause game
    // TODO: Add HandlePlayerDisconnect() - track disconnections
    // TODO: Add HandlePlayerReconnect() - rejoin in progress
    // TODO: Add GetSessionStatistics() - real-time stats for leaderboard
    // TODO: Add GenerateSessionSummary() - AI-generated recap
    // TODO: Add SaveSessionToDatabase() - persist completed sessions
    // TODO: Add LoadSessionFromDatabase() - for replays
    // TODO: Add CleanupOldSessions() - remove expired sessions
}
