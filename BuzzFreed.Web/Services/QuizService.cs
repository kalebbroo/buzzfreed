using BuzzFreed.Web.Models;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.AI.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace BuzzFreed.Web.Services
{
    public class QuizService
    {
        private readonly AIProviderRegistry _aiRegistry;
        private readonly DatabaseService _databaseService;
        private readonly ILogger<QuizService> _logger;

        // In-memory storage for active quiz sessions
        // In production, consider using Redis or similar distributed cache
        private static readonly ConcurrentDictionary<string, QuizSession> _activeSessions = new();

        public QuizService(
            AIProviderRegistry aiRegistry,
            DatabaseService databaseService,
            ILogger<QuizService> logger)
        {
            _aiRegistry = aiRegistry;
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

                // Get LLM provider
                var llmProvider = await _aiRegistry.GetLLMProviderAsync();
                if (llmProvider == null)
                {
                    throw new Exception("No LLM provider available");
                }

                _logger.LogInformation($"Using LLM provider: {llmProvider.ProviderName}");

                // Generate or use custom topic
                var topic = customTopic ?? await GenerateQuizTopicAsync(llmProvider);
                _logger.LogInformation($"Quiz topic: {topic}");

                // Generate questions
                var questions = await GenerateQuestionsAsync(llmProvider, topic, numberOfQuestions: 6);
                _logger.LogInformation($"Generated {questions.Count} questions");

                // Generate result personalities
                var personalities = await GenerateResultPersonalitiesAsync(llmProvider, topic);
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

                // Get LLM provider
                var llmProvider = await _aiRegistry.GetLLMProviderAsync();
                if (llmProvider == null)
                {
                    throw new Exception("No LLM provider available");
                }

                // Generate personalized description with AI
                var description = await GenerateResultDescriptionAsync(
                    llmProvider,
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

        // Private helper methods using AI Provider Registry

        private async Task<string> GenerateQuizTopicAsync(BuzzFreed.Web.AI.Abstractions.ILLMProvider provider)
        {
            var request = new LLMRequest
            {
                Prompt = @"Generate a fun, BuzzFeed-style quiz topic. It should be light-hearted and personality-based.
Examples: 'Which Type of Coffee Are You?', 'What Kind of Pizza Matches Your Personality?', 'Which Season Best Represents You?'
Only respond with the quiz topic, nothing else.",
                MaxTokens = 100,
                Temperature = 0.8
            };

            var response = await provider.GenerateCompletionAsync(request);
            return response.IsSuccess ? response.Text : "What's Your Personality Type?";
        }

        private async Task<List<Question>> GenerateQuestionsAsync(
            BuzzFreed.Web.AI.Abstractions.ILLMProvider provider,
            string topic,
            int numberOfQuestions = 6)
        {
            var request = new LLMRequest
            {
                Prompt = $@"Create {numberOfQuestions} fun, engaging questions for a BuzzFeed-style personality quiz titled '{topic}'.

For each question, provide:
1. A question text
2. Exactly 4 answer options labeled A, B, C, D
3. Each answer should lean toward a different personality result

Format your response as a JSON array of questions. Each question should have this structure:
{{
  ""text"": ""Question text here?"",
  ""options"": [
    ""A) First option"",
    ""B) Second option"",
    ""C) Third option"",
    ""D) Fourth option""
  ]
}}

Only respond with the JSON array, no other text.",
                MaxTokens = 1500,
                Temperature = 0.8
            };

            var response = await provider.GenerateCompletionAsync(request);

            if (!response.IsSuccess)
            {
                return GetFallbackQuestions(topic);
            }

            try
            {
                var questions = JsonConvert.DeserializeObject<List<QuestionDto>>(response.Text);
                return questions?.Select(q => new Question
                {
                    Text = q.Text,
                    Options = q.Options
                }).ToList() ?? GetFallbackQuestions(topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing questions from AI response");
                return GetFallbackQuestions(topic);
            }
        }

        private async Task<Dictionary<string, string>> GenerateResultPersonalitiesAsync(
            BuzzFreed.Web.AI.Abstractions.ILLMProvider provider,
            string topic)
        {
            var request = new LLMRequest
            {
                Prompt = $@"For a BuzzFeed-style quiz titled '{topic}', create 4 distinct personality result types.
Each result should correspond to answer patterns (mostly A's, mostly B's, mostly C's, or mostly D's).

Format your response as a JSON object with keys A, B, C, D and personality names as values.
Example for 'Which Coffee Are You?':
{{
  ""A"": ""Espresso"",
  ""B"": ""Latte"",
  ""C"": ""Cappuccino"",
  ""D"": ""Mocha""
}}

Only respond with the JSON object, no other text.",
                MaxTokens = 200,
                Temperature = 0.8
            };

            var response = await provider.GenerateCompletionAsync(request);

            if (!response.IsSuccess)
            {
                return GetFallbackPersonalities();
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Text)
                    ?? GetFallbackPersonalities();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing personalities from AI response");
                return GetFallbackPersonalities();
            }
        }

        private async Task<string> GenerateResultDescriptionAsync(
            BuzzFreed.Web.AI.Abstractions.ILLMProvider provider,
            string topic,
            string personality,
            List<string> userAnswers)
        {
            var answerSummary = string.Join(", ", userAnswers.GroupBy(a => a).Select(g => $"{g.Count()} {g.Key}'s"));

            var request = new LLMRequest
            {
                Prompt = $@"You are writing a fun, BuzzFeed-style quiz result.
Quiz topic: '{topic}'
Result personality: '{personality}'
User's answer pattern: {answerSummary}

Write a fun, engaging 2-3 sentence description for someone who got '{personality}' as their result.
Make it positive, relatable, and a bit playful. Don't mention the answer pattern directly.
Only respond with the description text, nothing else.",
                MaxTokens = 300,
                Temperature = 0.8,
                SystemMessage = "You are a creative assistant that helps create fun, engaging BuzzFeed-style quiz content."
            };

            var response = await provider.GenerateCompletionAsync(request);
            return response.IsSuccess ? response.Text :
                $"You're a {personality}! You have a unique and interesting personality that sets you apart from the crowd.";
        }

        // Fallback methods

        private List<Question> GetFallbackQuestions(string topic)
        {
            return new List<Question>
            {
                new Question
                {
                    Text = "How do you like to start your day?",
                    Options = new List<string>
                    {
                        "A) With high energy and excitement",
                        "B) Slow and steady",
                        "C) Creative and spontaneous",
                        "D) Organized and planned"
                    }
                },
                new Question
                {
                    Text = "What's your ideal weekend activity?",
                    Options = new List<string>
                    {
                        "A) Adventure and thrills",
                        "B) Relaxing at home",
                        "C) Trying something new",
                        "D) Catching up on tasks"
                    }
                }
            };
        }

        private Dictionary<string, string> GetFallbackPersonalities()
        {
            return new Dictionary<string, string>
            {
                { "A", "Dynamic Doer" },
                { "B", "Calm Collector" },
                { "C", "Creative Spirit" },
                { "D", "Organized Achiever" }
            };
        }

        // DTO for deserializing question JSON
        private class QuestionDto
        {
            [JsonProperty("text")]
            public string Text { get; set; } = string.Empty;

            [JsonProperty("options")]
            public List<string> Options { get; set; } = new List<string>();
        }
    }
}
