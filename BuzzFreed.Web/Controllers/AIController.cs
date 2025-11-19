using Microsoft.AspNetCore.Mvc;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIController(AIProviderRegistry registry) : ControllerBase
{
    public readonly AIProviderRegistry Registry = registry;

    /// <summary>
    /// Get all available AI providers
    /// GET /api/ai/providers
    /// </summary>
    [HttpGet("providers")]
    public ActionResult<List<ProviderInfo>> GetProviders()
    {
        try
        {
            List<ProviderInfo> providers = Registry.GetAllProvidersInfo();
            return Ok(providers);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error getting providers: {ex.Message}");
            return StatusCode(500, new { error = "Failed to get providers" });
        }
    }

    /// <summary>
    /// Get all available AI models organized by type
    /// GET /api/ai/models
    /// </summary>
    [HttpGet("models")]
    public ActionResult<ModelsResponse> GetModels([FromQuery] string? type = null)
    {
        try
        {
            if (!ValidationHelper.IsNullOrEmpty(type))
            {
                // Get models filtered by type
                if (Enum.TryParse<ModelType>(type, true, out ModelType modelType))
                {
                    List<AIModel> models = Registry.GetModelsByType(modelType);
                    return Ok(new ModelsResponse
                    {
                        Models = models,
                        TotalCount = models.Count
                    });
                }
                return BadRequest(new { error = "Invalid model type" });
            }

            // Get all models organized by type
            ModelsCatalog catalog = Registry.GetAllModels();
            return Ok(catalog);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error getting models: {ex.Message}");
            return StatusCode(500, new { error = "Failed to get models" });
        }
    }

    /// <summary>
    /// Get available models for a specific provider
    /// GET /api/ai/providers/{providerId}/models
    /// </summary>
    [HttpGet("providers/{providerId}/models")]
    public ActionResult<List<AIModel>> GetProviderModels(string providerId)
    {
        try
        {
            List<AIModel> models = Registry.GetProviderModels(providerId);
            if (models.Count == 0)
            {
                return NotFound(new { error = "Provider not found or has no models" });
            }
            return Ok(models);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error getting provider models: {ex.Message}");
            return StatusCode(500, new { error = "Failed to get provider models" });
        }
    }
}

// Response DTOs
public class ProviderInfo
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int ModelCount { get; set; }
}

public class ModelsResponse
{
    public List<AIModel> Models { get; set; } = new();
    public int TotalCount { get; set; }
}

public class ModelsCatalog
{
    public List<AIModel> LLMModels { get; set; } = new();
    public List<AIModel> ImageModels { get; set; } = new();
    public List<AIModel> AudioModels { get; set; } = new();
    public List<AIModel> VideoModels { get; set; } = new();
    public int TotalCount { get; set; }
}
