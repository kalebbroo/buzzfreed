using Microsoft.AspNetCore.Mvc;
using BuzzFreed.Web.Utils;
using Newtonsoft.Json;

namespace BuzzFreed.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IConfiguration configuration) : ControllerBase
    {
        public readonly IConfiguration Configuration = configuration;
        public readonly HttpClient HttpClient = HttpClientHelper.CreateClient();

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
                string clientId = ConfigHelper.GetValue(Configuration, "Discord:ClientId");
                string clientSecret = ConfigHelper.GetValue(Configuration, "Discord:ClientSecret");

                if (ValidationHelper.IsNullOrEmpty(clientId) || ValidationHelper.IsNullOrEmpty(clientSecret))
                {
                    Logs.Error("Discord credentials not configured");
                    return StatusCode(500, new { error = "Discord credentials not configured" });
                }

                // Exchange code for access token with Discord
                string tokenUrl = "https://discord.com/api/oauth2/token";
                Dictionary<string, string> formData = new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "grant_type", "authorization_code" },
                    { "code", request.Code }
                };

                HttpResult<DiscordTokenResponse> result = await HttpHelper.PostFormAsync<DiscordTokenResponse>(
                    HttpClient,
                    tokenUrl,
                    formData
                );

                if (!result.IsSuccess)
                {
                    Logs.Error($"Discord token exchange failed: {result.Error}");
                    return StatusCode(result.StatusCode, new { error = "Token exchange failed" });
                }

                if (result.Data == null || ValidationHelper.IsNullOrEmpty(result.Data.AccessToken))
                {
                    return StatusCode(500, new { error = "Invalid token response" });
                }

                return Ok(new TokenResponse
                {
                    AccessToken = result.Data.AccessToken
                });
            }
            catch (Exception ex)
            {
                Logs.Error($"Error exchanging token: {ex.Message}");
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
