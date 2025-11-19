using BuzzFreed.Web.Models;
using System.Collections.Concurrent;

namespace BuzzFreed.Web.Services
{
    public class QuizService
    {
        private readonly OpenAIService _openAIService;
        private readonly DatabaseService _databaseService;
        private readonly ILogger<QuizService> _logger;

        // In-memory storage for active quiz sessions
        // In production, consider using Redis or similar distributed cache
        private static readonly ConcurrentDictionary<string, QuizSession> _activeSessions = new();

        public QuizService(
            OpenAIService openAIService,
            DatabaseService databaseService,
            ILogger<QuizService> logger)
        {
            _openAIService = openAIService;
            _databaseService = databaseService;
            _logger = logger;
        }

        /// <summary>
        /// Generates a new quiz with AI and creates a session for the user
        /// </summary>
        public async Task<QuizSession> GenerateQuizAsync(string userId, string? customTopic = null)
        {
            try
            {
                _logger.LogInformation($"Generating quiz for user {userId}");

                // Generate or use custom topic
                var topic = customTopic ?? await _openAIService.GenerateQuizTopicAsync();
                _logger.LogInformation($"Quiz topic: {topic}");

                // Generate questions
                var questions = await _openAIService.GenerateQuestionsAsync(topic, numberOfQuestions: 6);
                _logger.LogInformation($"Generated {questions.Count} questions");

                // Generate result personalities
                var personalities = await _openAIService.GenerateResultPersonalitiesAsync(topic);
                _logger.LogInformation($"Generated {personalities.Count} personalities");

                // Create quiz
                var quiz = new Quiz
                {
                    Topic = topic,
                    Questions = questions,
                    ResultPersonalities = personalities
                };

                // Create session
                var session = new QuizSession
                {
                    UserId = userId,
                    Quiz = quiz,
                    UserAnswers = new List<string>(),
                    CurrentQuestionIndex = 0,
                    IsCompleted = false
                };

                // Store session
                _activeSessions[session.SessionId] = session;
                _logger.LogInformation($"Created session {session.SessionId} for user {userId}");

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quiz");
                throw;
            }
        }

        /// <summary>
        /// Records a user's answer to a question
        /// </summary>
        public QuizSession? SubmitAnswer(string sessionId, string answer)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning($"Session {sessionId} not found");
                return null;
            }

            // Validate answer (should be A, B, C, or D)
            answer = answer.ToUpper();
            if (!new[] { "A", "B", "C", "D" }.Contains(answer))
            {
                _logger.LogWarning($"Invalid answer: {answer}");
                return null;
            }

            // Record answer
            session.UserAnswers.Add(answer);
            session.CurrentQuestionIndex++;

            // Check if quiz is completed
            if (session.CurrentQuestionIndex >= session.Quiz.Questions.Count)
            {
                session.IsCompleted = true;
                _logger.LogInformation($"Quiz session {sessionId} completed");
            }

            return session;
        }

        /// <summary>
        /// Calculates the quiz result based on user answers
        /// </summary>
        public async Task<QuizResult> CalculateResultAsync(string sessionId, string userId, string guildId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            if (!session.IsCompleted)
            {
                throw new InvalidOperationException("Quiz is not completed yet");
            }

            try
            {
                // Calculate most common answer (A, B, C, or D)
                var mostCommonAnswer = session.UserAnswers
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;

                _logger.LogInformation($"Most common answer: {mostCommonAnswer}");

                // Get personality for that answer
                var personality = session.Quiz.ResultPersonalities.GetValueOrDefault(mostCommonAnswer, "Unknown");

                // Generate personalized description with AI
                var description = await _openAIService.GenerateResultDescriptionAsync(
                    session.Quiz.Topic,
                    personality,
                    session.UserAnswers
                );

                // Create result
                var result = new QuizResult
                {
                    UserId = userId,
                    DiscordGuildId = guildId,
                    QuizId = session.Quiz.Id,
                    QuizTopic = session.Quiz.Topic,
                    UserAnswers = session.UserAnswers,
                    ResultPersonality = personality,
                    ResultDescription = description
                };

                // Save to database
                await _databaseService.SaveQuizResultAsync(result);

                // Clean up session
                _activeSessions.TryRemove(sessionId, out _);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating quiz result");
                throw;
            }
        }

        /// <summary>
        /// Gets an active quiz session
        /// </summary>
        public QuizSession? GetSession(string sessionId)
        {
            _activeSessions.TryGetValue(sessionId, out var session);
            return session;
        }

        /// <summary>
        /// Gets user's quiz history from database
        /// </summary>
        public async Task<List<QuizResult>> GetUserHistoryAsync(string userId, string guildId)
        {
            return await _databaseService.GetUserQuizHistoryAsync(userId, guildId);
        }
    }
}
