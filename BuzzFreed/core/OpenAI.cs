using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenAI
{
    public class GptChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiEndpoint;

        public GptChatService(string apiKey)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _apiEndpoint = "https://api.openai.com/v1/engines/davinci-codex/completions";
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> GetGptResponseAsync(string prompt, int maxTokens = 150)
        {
            var payload = new
            {
                prompt = prompt,
                max_tokens = maxTokens
            };

            var payloadString = JsonConvert.SerializeObject(payload);
            var httpContent = new StringContent(payloadString, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiEndpoint, httpContent);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObject = JObject.Parse(responseContent);
                return responseObject["choices"]?.First?["text"]?.ToString().Trim() ?? string.Empty;
            }
            else
            {
                // Handle error
                Console.WriteLine($"Failed to get GPT-3.5 Turbo response: {response.StatusCode}");
                return string.Empty;
            }
        }
    }
}
