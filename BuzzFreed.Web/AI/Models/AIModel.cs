namespace BuzzFreed.Web.AI.Models;

/// <summary>
/// Represents a specific AI model from a provider
/// </summary>
public class AIModel
{
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public ModelType Type { get; set; }
    public bool IsAvailable { get; set; }
    public int Priority { get; set; }
    public ModelCapabilities Capabilities { get; set; } = new();
    public ModelPricing? Pricing { get; set; }
}

/// <summary>
/// Model type classification
/// </summary>
public enum ModelType
{
    LLM,
    Image,
    Audio,
    Video,
    Embedding
}

/// <summary>
/// Model-specific capabilities
/// </summary>
public class ModelCapabilities
{
    public int MaxTokens { get; set; }
    public int MaxImageSize { get; set; }
    public bool SupportsStreaming { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public List<string> SupportedFormats { get; set; } = new();
    public Dictionary<string, object> CustomCapabilities { get; set; } = new();
}

/// <summary>
/// Model pricing information
/// </summary>
public class ModelPricing
{
    public decimal InputCostPer1kTokens { get; set; }
    public decimal OutputCostPer1kTokens { get; set; }
    public decimal ImageCostPerGeneration { get; set; }
    public string Currency { get; set; } = "USD";
}
