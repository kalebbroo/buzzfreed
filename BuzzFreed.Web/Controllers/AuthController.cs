using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft.Json;

namespace BuzzFreed.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly HttpClient _httpClient;

        public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Exchange Discord OAuth code for access token
        /// POST /api/auth/token
        /// This endpoint is called by the Discord Embedded App SDK
        /// </summary>
        [HttpPost("token")]
        public async Task<ActionResult<TokenResponse>> ExchangeToken([FromBody] TokenRequest request)
        {
            try
            {
                var clientId = _configuration["Discord:ClientId"];
                var clientSecret = _configuration["Discord:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("Discord credentials not configured");
                    return StatusCode(500, new { error = "Discord credentials not configured" });
                }

                // Exchange code for access token with Discord
                var tokenUrl = "https://discord.com/api/oauth2/token";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "grant_type", "authorization_code" },
                    { "code", request.Code }
                });

                var response = await _httpClient.PostAsync(tokenUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Discord token exchange failed: {responseContent}");
                    return StatusCode((int)response.StatusCode, new { error = "Token exchange failed" });
                }

                var tokenData = JsonConvert.DeserializeObject<DiscordTokenResponse>(responseContent);

                if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
                {
                    return StatusCode(500, new { error = "Invalid token response" });
                }

                return Ok(new TokenResponse
                {
                    AccessToken = tokenData.AccessToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging token");
                return StatusCode(500, new { error = "Failed to exchange token" });
            }
        }
    }

    // Request/Response DTOs
    public class TokenRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;
    }

    // Discord API response
    public class DiscordTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonProperty("scope")]
        public string Scope { get; set; } = string.Empty;
    }
}
