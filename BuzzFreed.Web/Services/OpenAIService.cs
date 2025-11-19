using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BuzzFreed.Web.Models;

namespace BuzzFreed.Web.Services
{
    public class OpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<OpenAIService> _logger;
        private const string ApiEndpoint = "https://api.openai.com/v1/chat/completions";
        private const string Model = "gpt-4o-mini"; // Using the faster, cheaper model

        public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentException("OpenAI API key not configured");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _logger = logger;
        }

        /// <summary>
        /// Generates a random BuzzFeed-style quiz topic
        /// </summary>
        public async Task<string> GenerateQuizTopicAsync()
        {
            var prompt = @"Generate a fun, BuzzFeed-style quiz topic. It should be light-hearted and personality-based.
Examples: 'Which Type of Coffee Are You?', 'What Kind of Pizza Matches Your Personality?', 'Which Season Best Represents You?'
Only respond with the quiz topic, nothing else.";

            return await GetChatCompletionAsync(prompt);
        }

        /// <summary>
        /// Generates quiz questions with 4 multiple choice answers for a given topic
        /// </summary>
        public async Task<List<Question>> GenerateQuestionsAsync(string topic, int numberOfQuestions = 6)
        {
            var prompt = $@"Create {numberOfQuestions} fun, engaging questions for a BuzzFeed-style personality quiz titled '{topic}'.

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

Only respond with the JSON array, no other text.";

            var response = await GetChatCompletionAsync(prompt);

            try
            {
                var questions = JsonConvert.DeserializeObject<List<QuestionDto>>(response);
                return questions?.Select(q => new Question
                {
                    Text = q.Text,
                    Options = q.Options
                }).ToList() ?? new List<Question>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing questions from OpenAI response");
                return GetFallbackQuestions(topic);
            }
        }

        /// <summary>
        /// Generates personality result types for the quiz
        /// </summary>
        public async Task<Dictionary<string, string>> GenerateResultPersonalitiesAsync(string topic)
        {
            var prompt = $@"For a BuzzFeed-style quiz titled '{topic}', create 4 distinct personality result types.
Each result should correspond to answer patterns (mostly A's, mostly B's, mostly C's, or mostly D's).

Format your response as a JSON object with keys A, B, C, D and personality names as values.
Example for 'Which Coffee Are You?':
{{
  ""A"": ""Espresso"",
  ""B"": ""Latte"",
  ""C"": ""Cappuccino"",
  ""D"": ""Mocha""
}}

Only respond with the JSON object, no other text.";

            var response = await GetChatCompletionAsync(prompt);

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(response)
                    ?? GetFallbackPersonalities();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing personalities from OpenAI response");
                return GetFallbackPersonalities();
            }
        }

        /// <summary>
        /// Generates a personalized result description based on the personality type
        /// </summary>
        public async Task<string> GenerateResultDescriptionAsync(string topic, string personality, List<string> userAnswers)
        {
            var answerSummary = string.Join(", ", userAnswers.GroupBy(a => a).Select(g => $"{g.Count()} {g.Key}'s"));

            var prompt = $@"You are writing a fun, BuzzFeed-style quiz result.
Quiz topic: '{topic}'
Result personality: '{personality}'
User's answer pattern: {answerSummary}

Write a fun, engaging 2-3 sentence description for someone who got '{personality}' as their result.
Make it positive, relatable, and a bit playful. Don't mention the answer pattern directly.
Only respond with the description text, nothing else.";

            return await GetChatCompletionAsync(prompt);
        }

        /// <summary>
        /// Makes a ChatGPT API call and returns the response text
        /// </summary>
        private async Task<string> GetChatCompletionAsync(string userMessage, int maxTokens = 500)
        {
            try
            {
                var payload = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a creative assistant that helps create fun, engaging BuzzFeed-style quiz content." },
                        new { role = "user", content = userMessage }
                    },
                    max_tokens = maxTokens,
                    temperature = 0.8 // Higher temperature for more creative responses
                };

                var payloadString = JsonConvert.SerializeObject(payload);
                var httpContent = new StringContent(payloadString, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ApiEndpoint, httpContent);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObject = JObject.Parse(responseContent);

                var message = responseObject["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();

                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogWarning("Empty response from OpenAI API");
                    return string.Empty;
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                throw;
            }
        }

        // Fallback methods for when AI fails

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
