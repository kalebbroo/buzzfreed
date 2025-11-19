using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.AI.Registry;

/// <summary>
/// Central registry for managing all AI providers
/// </summary>
public class AIProviderRegistry(IConfiguration configuration)
{
    public readonly AIProvidersConfig Config = InitializeConfig(configuration);
    public readonly Dictionary<string, ILLMProvider> LLMProviders = new();
    public readonly Dictionary<string, IImageProvider> ImageProviders = new();

    public static AIProvidersConfig InitializeConfig(IConfiguration configuration)
    {
        AIProvidersConfig config = new();
        configuration.GetSection("AI:Providers").Bind(config);
        return config;
    }

    /// <summary>
    /// Register an LLM provider
    /// </summary>
    public void RegisterLLMProvider(ILLMProvider provider)
    {
        LLMProviders[provider.ProviderId] = provider;
        Logs.Init($"Registered LLM provider: {provider.ProviderName} ({provider.ProviderId})");
    }

    /// <summary>
    /// Register an Image provider
    /// </summary>
    public void RegisterImageProvider(IImageProvider provider)
    {
        ImageProviders[provider.ProviderId] = provider;
        Logs.Init($"Registered Image provider: {provider.ProviderName} ({provider.ProviderId})");
    }

    /// <summary>
    /// Get best available LLM provider
    /// </summary>
    public async Task<ILLMProvider?> GetLLMProviderAsync(string? preferredProviderId = null)
    {
        // Try preferred provider first
        if (!ValidationHelper.IsNullOrEmpty(preferredProviderId) && LLMProviders.ContainsKey(preferredProviderId))
        {
            ILLMProvider preferred = LLMProviders[preferredProviderId];
            if (await preferred.IsAvailableAsync())
            {
                return preferred;
            }
        }

        // Try default provider
        if (LLMProviders.ContainsKey(Config.DefaultLLMProvider))
        {
            ILLMProvider defaultProvider = LLMProviders[Config.DefaultLLMProvider];
            if (await defaultProvider.IsAvailableAsync())
            {
                return defaultProvider;
            }
        }

        // Fallback: try any available provider by priority
        if (Config.EnableFallback)
        {
            List<ILLMProvider> sortedProviders = LLMProviders.Values
                .OrderByDescending(p => GetProviderPriority(p.ProviderId))
                .ToList();

            foreach (ILLMProvider provider in sortedProviders)
            {
                if (await provider.IsAvailableAsync())
                {
                    Logs.Warning($"Using fallback LLM provider: {provider.ProviderName}");
                    return provider;
                }
            }
        }

        Logs.Error("No available LLM providers found");
        return null;
    }

    /// <summary>
    /// Get best available Image provider
    /// </summary>
    public async Task<IImageProvider?> GetImageProviderAsync(string? preferredProviderId = null)
    {
        // Try preferred provider first
        if (!ValidationHelper.IsNullOrEmpty(preferredProviderId) && ImageProviders.ContainsKey(preferredProviderId))
        {
            IImageProvider preferred = ImageProviders[preferredProviderId];
            if (await preferred.IsAvailableAsync())
            {
                return preferred;
            }
        }

        // Try default provider
        if (ImageProviders.ContainsKey(Config.DefaultImageProvider))
        {
            IImageProvider defaultProvider = ImageProviders[Config.DefaultImageProvider];
            if (await defaultProvider.IsAvailableAsync())
            {
                return defaultProvider;
            }
        }

        // Fallback: try any available provider by priority
        if (Config.EnableFallback)
        {
            List<IImageProvider> sortedProviders = ImageProviders.Values
                .OrderByDescending(p => GetProviderPriority(p.ProviderId))
                .ToList();

            foreach (IImageProvider provider in sortedProviders)
            {
                if (await provider.IsAvailableAsync())
                {
                    Logs.Warning($"Using fallback Image provider: {provider.ProviderName}");
                    return provider;
                }
            }
        }

        Logs.Error("No available Image providers found");
        return null;
    }

    /// <summary>
    /// Get all registered LLM providers
    /// </summary>
    public IEnumerable<ILLMProvider> GetAllLLMProviders() => LLMProviders.Values;

    /// <summary>
    /// Get all registered Image providers
    /// </summary>
    public IEnumerable<IImageProvider> GetAllImageProviders() => ImageProviders.Values;

    /// <summary>
    /// Get provider configuration
    /// </summary>
    public AIProviderConfig? GetProviderConfig(string providerId)
    {
        return Config.Providers.FirstOrDefault(p => p.ProviderId == providerId);
    }

    /// <summary>
    /// Get provider priority from config
    /// </summary>
    public int GetProviderPriority(string providerId)
    {
        AIProviderConfig? config = GetProviderConfig(providerId);
        return config?.Priority ?? 0;
    }

    /// <summary>
    /// Check if a provider is enabled
    /// </summary>
    public bool IsProviderEnabled(string providerId)
    {
        AIProviderConfig? config = GetProviderConfig(providerId);
        return config?.Enabled ?? false;
    }

    /// <summary>
    /// Get all available models from all providers
    /// </summary>
    public Controllers.ModelsCatalog GetAllModels()
    {
        Controllers.ModelsCatalog catalog = new Controllers.ModelsCatalog();

        // Get models from all LLM providers
        foreach (ILLMProvider provider in LLMProviders.Values)
        {
            List<AIModel> models = provider.GetAvailableModels();
            catalog.LLMModels.AddRange(models);
        }

        // Get models from all Image providers
        foreach (IImageProvider provider in ImageProviders.Values)
        {
            List<AIModel> models = provider.GetAvailableModels();
            catalog.ImageModels.AddRange(models);
        }

        // Sort by priority (descending)
        catalog.LLMModels = catalog.LLMModels.OrderByDescending(m => m.Priority).ToList();
        catalog.ImageModels = catalog.ImageModels.OrderByDescending(m => m.Priority).ToList();

        catalog.TotalCount = catalog.LLMModels.Count + catalog.ImageModels.Count +
                           catalog.AudioModels.Count + catalog.VideoModels.Count;

        Logs.Info($"Retrieved {catalog.TotalCount} total AI models ({catalog.LLMModels.Count} LLM, {catalog.ImageModels.Count} Image)");

        return catalog;
    }

    /// <summary>
    /// Get models filtered by type
    /// </summary>
    public List<AIModel> GetModelsByType(ModelType type)
    {
        List<AIModel> models = new List<AIModel>();

        switch (type)
        {
            case ModelType.LLM:
                foreach (ILLMProvider provider in LLMProviders.Values)
                {
                    models.AddRange(provider.GetAvailableModels());
                }
                break;

            case ModelType.Image:
                foreach (IImageProvider provider in ImageProviders.Values)
                {
                    models.AddRange(provider.GetAvailableModels());
                }
                break;

            default:
                Logs.Warning($"Model type {type} not yet implemented");
                break;
        }

        return models.OrderByDescending(m => m.Priority).ToList();
    }

    /// <summary>
    /// Get models from a specific provider
    /// </summary>
    public List<AIModel> GetProviderModels(string providerId)
    {
        // Check LLM providers
        if (LLMProviders.ContainsKey(providerId))
        {
            return LLMProviders[providerId].GetAvailableModels();
        }

        // Check Image providers
        if (ImageProviders.ContainsKey(providerId))
        {
            return ImageProviders[providerId].GetAvailableModels();
        }

        return new List<AIModel>();
    }

    /// <summary>
    /// Get information about all registered providers
    /// </summary>
    public List<Controllers.ProviderInfo> GetAllProvidersInfo()
    {
        List<Controllers.ProviderInfo> providersInfo = new List<Controllers.ProviderInfo>();

        // Add LLM providers
        foreach (ILLMProvider provider in LLMProviders.Values)
        {
            providersInfo.Add(new Controllers.ProviderInfo
            {
                ProviderId = provider.ProviderId,
                ProviderName = provider.ProviderName,
                Type = "LLM",
                IsAvailable = provider.IsAvailableAsync().Result,
                ModelCount = provider.GetAvailableModels().Count
            });
        }

        // Add Image providers
        foreach (IImageProvider provider in ImageProviders.Values)
        {
            providersInfo.Add(new Controllers.ProviderInfo
            {
                ProviderId = provider.ProviderId,
                ProviderName = provider.ProviderName,
                Type = "Image",
                IsAvailable = provider.IsAvailableAsync().Result,
                ModelCount = provider.GetAvailableModels().Count
            });
        }

        return providersInfo.OrderByDescending(p => p.IsAvailable).ThenBy(p => p.ProviderName).ToList();
    }
}
