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
        if (!string.IsNullOrEmpty(preferredProviderId) && LLMProviders.ContainsKey(preferredProviderId))
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
        if (!string.IsNullOrEmpty(preferredProviderId) && ImageProviders.ContainsKey(preferredProviderId))
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
}
