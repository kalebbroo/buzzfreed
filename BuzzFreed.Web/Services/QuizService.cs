using BuzzFreed.Web.Models;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.Utils;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace BuzzFreed.Web.Services;

public class QuizService(
    AIProviderRegistry aiRegistry,
    DatabaseService databaseService)
{
    public readonly AIProviderRegistry AiRegistry = aiRegistry;
    public readonly DatabaseService DatabaseService = databaseService;

    // In-memory storage for active quiz sessions
    // In production, consider using Redis or similar distributed cache
    public static readonly ConcurrentDictionary<string, QuizSession> ActiveSessions = new();

        /// <summary>
        /// Generates a new quiz with AI and creates a session for the user
        /// </summary>
        public async Task<QuizSession> GenerateQuizAsync(string userId, string? customTopic = null)
        {
            try
            {
                Logs.Info($"Generating quiz for user {userId}");

                // Get LLM provider
                BuzzFreed.Web.AI.Abstractions.ILLMProvider? llmProvider = await AiRegistry.GetLLMProviderAsync();
                if (llmProvider == null)
                {
                    throw new Exception("No LLM provider available");
                }

                Logs.Info($"Using LLM provider: {llmProvider.ProviderName}");

                // Generate or use custom topic
                string topic = customTopic ?? await GenerateQuizTopicAsync(llmProvider);
                Logs.Info($"Quiz topic: {topic}");

                // Generate questions
                List<Question> questions = await GenerateQuestionsAsync(llmProvider, topic, numberOfQuestions: 6);
                Logs.Info($"Generated {questions.Count} questions");

                // Generate result personalities
                Dictionary<string, string> personalities = await GenerateResultPersonalitiesAsync(llmProvider, topic);
                Logs.Info($"Generated {personalities.Count} personalities");

                // Create quiz
                Quiz quiz = new()
                {
                    Topic = topic,
                    Questions = questions,
                    ResultPersonalities = personalities
                };

                // Create session
                QuizSession session = new()
                {
                    UserId = userId,
                    Quiz = quiz,
                    UserAnswers = new List<string>(),
                    CurrentQuestionIndex = 0,
                    IsCompleted = false
                };

                // Store session
                ActiveSessions[session.SessionId] = session;
                Logs.Info($"Created session {session.SessionId} for user {userId}");

                return session;
            }
            catch (Exception ex)
            {
                Logs.Error($"Error generating quiz: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Records a user's answer to a question
        /// </summary>
        public QuizSession? SubmitAnswer(string sessionId, string answer)
        {
            if (!ActiveSessions.TryGetValue(sessionId, out QuizSession? session))
            {
                Logs.Warning($"Session {sessionId} not found");
                return null;
            }

            // Validate answer (should be A, B, C, or D)
            answer = answer.ToUpper();
            if (!new[] { "A", "B", "C", "D" }.Contains(answer))
            {
                Logs.Warning($"Invalid answer: {answer}");
                return null;
            }

            // Record answer
            session.UserAnswers.Add(answer);
            session.CurrentQuestionIndex++;

            // Check if quiz is completed
            if (session.CurrentQuestionIndex >= session.Quiz.Questions.Count)
            {
                session.IsCompleted = true;
                Logs.Info($"Quiz session {sessionId} completed");
            }

            return session;
        }

        /// <summary>
        /// Calculates the quiz result based on user answers
        /// </summary>
        public async Task<QuizResult> CalculateResultAsync(string sessionId, string userId, string guildId)
        {
            if (!ActiveSessions.TryGetValue(sessionId, out QuizSession? session))
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
                string mostCommonAnswer = session.UserAnswers
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;

                Logs.Info($"Most common answer: {mostCommonAnswer}");

                // Get personality for that answer
                string personality = session.Quiz.ResultPersonalities.GetValueOrDefault(mostCommonAnswer, "Unknown");

                // Get LLM provider
                BuzzFreed.Web.AI.Abstractions.ILLMProvider? llmProvider = await AiRegistry.GetLLMProviderAsync();
                if (llmProvider == null)
                {
                    throw new Exception("No LLM provider available");
                }

                // Generate personalized description with AI
                string description = await GenerateResultDescriptionAsync(
                    llmProvider,
                    session.Quiz.Topic,
                    personality,
                    session.UserAnswers
                );

                // Create result
                QuizResult result = new QuizResult
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
                await DatabaseService.SaveQuizResultAsync(result);

                // Clean up session
                ActiveSessions.TryRemove(sessionId, out _);

                return result;
            }
            catch (Exception ex)
            {
                Logs.Error($"Error calculating quiz result: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets an active quiz session
        /// </summary>
        public QuizSession? GetSession(string sessionId)
        {
            ActiveSessions.TryGetValue(sessionId, out QuizSession? session);
            return session;
        }

        /// <summary>
        /// Gets user's quiz history from database
        /// </summary>
        public async Task<List<QuizResult>> GetUserHistoryAsync(string userId, string guildId)
        {
            return await DatabaseService.GetUserQuizHistoryAsync(userId, guildId);
        }

        // Helper methods using AI Provider Registry

        public async Task<string> GenerateQuizTopicAsync(BuzzFreed.Web.AI.Abstractions.ILLMProvider provider)
        {
            LLMRequest request = new LLMRequest
            {
                Prompt = @"Generate a fun, BuzzFeed-style quiz topic. It should be light-hearted and personality-based.
Examples: 'Which Type of Coffee Are You?', 'What Kind of Pizza Matches Your Personality?', 'Which Season Best Represents You?'
Only respond with the quiz topic, nothing else.",
                MaxTokens = 100,
                Temperature = 0.8
            };

            LLMResponse response = await provider.GenerateCompletionAsync(request);
            return response.IsSuccess ? response.Text : "What's Your Personality Type?";
        }

        public async Task<List<Question>> GenerateQuestionsAsync(
            BuzzFreed.Web.AI.Abstractions.ILLMProvider provider,
            string topic,
            int numberOfQuestions = 6)
        {
            LLMRequest request = new LLMRequest
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

            LLMResponse response = await provider.GenerateCompletionAsync(request);

            if (!response.IsSuccess)
            {
                return GetFallbackQuestions(topic);
            }

            try
            {
                List<QuestionDto>? questions = JsonConvert.DeserializeObject<List<QuestionDto>>(response.Text);
                return questions?.Select(q => new Question
                {
                    Text = q.Text,
                    Options = q.Options
                }).ToList() ?? GetFallbackQuestions(topic);
            }
            catch (Exception ex)
            {
                Logs.Error($"Error parsing questions from AI response: {ex.Message}");
                return GetFallbackQuestions(topic);
            }
        }

        public async Task<Dictionary<string, string>> GenerateResultPersonalitiesAsync(
            BuzzFreed.Web.AI.Abstractions.ILLMProvider provider,
            string topic)
        {
            LLMRequest request = new LLMRequest
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

            LLMResponse response = await provider.GenerateCompletionAsync(request);

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
                Logs.Error($"Error parsing personalities from AI response: {ex.Message}");
                return GetFallbackPersonalities();
            }
        }

        public async Task<string> GenerateResultDescriptionAsync(
            BuzzFreed.Web.AI.Abstractions.ILLMProvider provider,
            string topic,
            string personality,
            List<string> userAnswers)
        {
            string answerSummary = string.Join(", ", userAnswers.GroupBy(a => a).Select(g => $"{g.Count()} {g.Key}'s"));

            LLMRequest request = new LLMRequest
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

            LLMResponse response = await provider.GenerateCompletionAsync(request);
            return response.IsSuccess ? response.Text :
                $"You're a {personality}! You have a unique and interesting personality that sets you apart from the crowd.";
        }

        // Fallback methods

        public List<Question> GetFallbackQuestions(string topic)
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

        public Dictionary<string, string> GetFallbackPersonalities()
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
        public class QuestionDto
        {
            [JsonProperty("text")]
            public string Text { get; set; } = string.Empty;

            [JsonProperty("options")]
            public List<string> Options { get; set; } = new List<string>();
        }
    }
}
