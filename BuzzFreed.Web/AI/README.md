# 🤖 BuzzFreed AI Provider System

A modular, extensible system for integrating multiple LLM and Image Generation AI providers.

## 📋 Overview

The AI Provider System allows BuzzFreed to work with multiple AI services through a unified interface. It includes:

- **Registry-based architecture** - Central management of all AI providers
- **Automatic fallback** - If primary provider fails, automatically tries alternatives
- **Priority system** - Configure which providers to prefer
- **Easy extensibility** - Add new providers with minimal code changes
- **Configuration-driven** - Enable/disable providers via appsettings.json

## 🏗️ Architecture

```
┌─────────────────────────────────────┐
│     QuizService (or other service)  │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│      AIProviderRegistry             │
│  - Manages all providers            │
│  - Selects best available           │
│  - Handles fallbacks                │
└──────────────┬──────────────────────┘
               │
      ┌────────┴────────┐
      ▼                 ▼
┌──────────┐      ┌──────────┐
│   LLM    │      │  Image   │
│Providers │      │Providers │
└────┬─────┘      └────┬─────┘
     │                 │
   ┌─┴──────┐      ┌──┴────────┐
   │OpenAI  │      │OpenAI     │
   │        │      │DALL-E     │
   └────────┘      └───────────┘
                   │SwarmUI    │
                   │           │
                   └───────────┘
```

## 📂 Project Structure

```
AI/
├── Abstractions/                # Interfaces
│   ├── IAIProvider.cs          # Base provider interface
│   ├── ILLMProvider.cs         # LLM-specific interface
│   └── IImageProvider.cs       # Image generation interface
│
├── Models/                      # Data models
│   ├── LLMRequest.cs           # LLM request parameters
│   ├── LLMResponse.cs          # LLM response data
│   ├── ImageRequest.cs         # Image generation request
│   ├── ImageResponse.cs        # Image generation response
│   ├── ChatMessage.cs          # Chat message model
│   └── ProviderCapabilities.cs # Provider capability descriptor
│
├── Providers/                   # Provider implementations
│   ├── OpenAI/
│   │   ├── OpenAILLMProvider.cs       # GPT-4, GPT-3.5, etc.
│   │   └── OpenAIImageProvider.cs     # DALL-E 2, DALL-E 3
│   └── SwarmUI/
│       └── SwarmUIImageProvider.cs    # Stable Diffusion via SwarmUI
│
└── Registry/                    # Registry system
    ├── AIProviderRegistry.cs   # Main registry class
    └── AIProviderConfig.cs     # Configuration models
```

## 🚀 Usage

### Basic LLM Usage

```csharp
public class MyService
{
    private readonly AIProviderRegistry _aiRegistry;

    public MyService(AIProviderRegistry aiRegistry)
    {
        _aiRegistry = aiRegistry;
    }

    public async Task<string> GenerateTextAsync(string prompt)
    {
        // Get best available LLM provider
        var provider = await _aiRegistry.GetLLMProviderAsync();

        if (provider == null)
        {
            throw new Exception("No LLM provider available");
        }

        // Create request
        var request = new LLMRequest
        {
            Prompt = prompt,
            MaxTokens = 500,
            Temperature = 0.8
        };

        // Generate completion
        var response = await provider.GenerateCompletionAsync(request);

        return response.IsSuccess ? response.Text : "Error generating text";
    }
}
```

### Chat Completion

```csharp
var messages = new List<ChatMessage>
{
    ChatMessage.System("You are a helpful assistant."),
    ChatMessage.User("What is the capital of France?")
};

var request = new LLMRequest { MaxTokens = 100 };
var response = await provider.GenerateChatCompletionAsync(messages, request);
```

### Image Generation

```csharp
// Get best available image provider
var provider = await _aiRegistry.GetImageProviderAsync();

// Create image request
var request = new ImageRequest
{
    Prompt = "A beautiful sunset over mountains",
    Width = 1024,
    Height = 1024,
    Count = 1
};

// Generate image
var response = await provider.GenerateImageAsync(request);

if (response.IsSuccess)
{
    var imageUrl = response.Images[0].Url;
    // Use the image...
}
```

### Image Generation with Progress

```csharp
var response = await provider.GenerateImageWithProgressAsync(
    request,
    progressCallback: (percent, message) =>
    {
        Console.WriteLine($"{percent}%: {message}");
    }
);
```

### Preferred Provider

```csharp
// Try to use a specific provider, fallback to others if unavailable
var provider = await _aiRegistry.GetLLMProviderAsync("openai");
```

## ⚙️ Configuration

### appsettings.json

```json
{
  "AI": {
    "Providers": {
      "DefaultLLMProvider": "openai",
      "DefaultImageProvider": "openai-image",
      "EnableFallback": true,
      "Providers": [
        {
          "ProviderId": "openai",
          "Enabled": true,
          "Priority": 100,
          "ApiKey": "${OPENAI_API_KEY}",
          "DefaultModel": "gpt-4o-mini",
          "TimeoutSeconds": 60,
          "MaxRetries": 2
        },
        {
          "ProviderId": "openai-image",
          "Enabled": true,
          "Priority": 90,
          "ApiKey": "${OPENAI_API_KEY}",
          "DefaultModel": "dall-e-3",
          "TimeoutSeconds": 120,
          "MaxRetries": 1
        },
        {
          "ProviderId": "swarmui",
          "Enabled": false,
          "Priority": 80,
          "BaseUrl": "${SWARMUI_URL:-http://127.0.0.1:7801}",
          "DefaultModel": "OfficialStableDiffusion/sd_xl_base_1.0",
          "TimeoutSeconds": 180,
          "MaxRetries": 2
        }
      ]
    }
  }
}
```

### Environment Variables

```.env
# OpenAI API Key
OPENAI_API_KEY=sk-...

# SwarmUI URL (optional)
SWARMUI_URL=http://localhost:7801
```

### Configuration Options

| Field | Description |
|-------|-------------|
| `ProviderId` | Unique identifier for the provider |
| `Enabled` | Whether the provider is active |
| `Priority` | Higher = more preferred (used for fallback) |
| `ApiKey` | API authentication key |
| `BaseUrl` | Custom API endpoint URL |
| `DefaultModel` | Model to use by default |
| `TimeoutSeconds` | Request timeout |
| `MaxRetries` | Number of retry attempts |

## 🔌 Available Providers

### OpenAI LLM Provider (`openai`)

**Supported Models:**
- `gpt-4o`
- `gpt-4o-mini`
- `gpt-4-turbo`
- `gpt-4`
- `gpt-3.5-turbo`

**Capabilities:**
- ✅ Chat completions
- ✅ Streaming (not yet implemented)
- ✅ Function calling
- ✅ Vision (multimodal)
- ⚠️ Max context: 128K tokens (gpt-4o)

### OpenAI Image Provider (`openai-image`)

**Supported Models:**
- `dall-e-3`
- `dall-e-2`

**Capabilities:**
- ✅ Text-to-image generation
- ✅ HD quality option
- ✅ Style control (vivid, natural)
- ⚠️ DALL-E 3: Max 1 image per request
- ⚠️ DALL-E 2: Max 10 images per request

**Supported Sizes:**
- `1024x1024`, `1792x1024`, `1024x1792` (DALL-E 3)
- `256x256`, `512x512`, `1024x1024` (DALL-E 2)

### SwarmUI Image Provider (`swarmui`)

**Description:** Stable Diffusion models via SwarmUI

**Supported Models:**
- Any model available in your SwarmUI installation
- Default: `OfficialStableDiffusion/sd_xl_base_1.0`

**Capabilities:**
- ✅ Text-to-image generation
- ✅ Negative prompts
- ✅ CFG scale control
- ✅ Step count control
- ✅ Seed control (reproducible)
- ✅ Progress updates via WebSocket
- ✅ Multiple images per request

**Supported Sizes:**
- Flexible: Any size your GPU can handle
- Common: `512x512`, `768x768`, `1024x1024`, `1024x768`, etc.

## 🔧 Adding a New Provider

### 1. Create Provider Class

```csharp
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;

namespace BuzzFreed.Web.AI.Providers.MyProvider
{
    public class MyLLMProvider : ILLMProvider
    {
        public string ProviderId => "myprovider";
        public string ProviderName => "My Provider";
        public ProviderType Type => ProviderType.LLM;

        public List<string> SupportedModels => new List<string>
        {
            "model-1", "model-2"
        };

        public Task<bool> IsAvailableAsync()
        {
            // Check if provider is configured and reachable
            return Task.FromResult(true);
        }

        public ProviderCapabilities GetCapabilities()
        {
            return new ProviderCapabilities
            {
                SupportsChat = true,
                MaxTokens = 4096,
                // ... other capabilities
            };
        }

        public async Task<LLMResponse> GenerateCompletionAsync(
            LLMRequest request,
            CancellationToken cancellationToken = default)
        {
            // Implement your provider logic here
            // Call the API, parse response, return LLMResponse
        }

        public async Task<LLMResponse> GenerateChatCompletionAsync(
            List<ChatMessage> messages,
            LLMRequest request,
            CancellationToken cancellationToken = default)
        {
            // Implement chat completion logic
        }
    }
}
```

### 2. Register in Program.cs

```csharp
// Register provider
builder.Services.AddSingleton<MyLLMProvider>();

// ... later in the app initialization
var myProvider = scope.ServiceProvider.GetRequiredService<MyLLMProvider>();
registry.RegisterLLMProvider(myProvider);
```

### 3. Add Configuration

```json
{
  "AI": {
    "Providers": {
      "Providers": [
        {
          "ProviderId": "myprovider",
          "Enabled": true,
          "Priority": 95,
          "ApiKey": "${MY_PROVIDER_API_KEY}",
          "DefaultModel": "model-1"
        }
      ]
    }
  }
}
```

## 🎯 Best Practices

1. **Always check IsSuccess** on responses before using data
2. **Implement fallback logic** for critical operations
3. **Use appropriate timeouts** based on provider characteristics
4. **Monitor token usage** for cost control
5. **Cache responses** where appropriate to reduce API calls
6. **Handle rate limits** gracefully
7. **Log provider selection** for debugging

## 🐛 Troubleshooting

### No LLM Provider Available

**Cause:** No providers are configured or all are disabled/unavailable

**Solution:**
1. Check `appsettings.json` - ensure at least one provider is `Enabled: true`
2. Verify API keys are set in environment variables
3. Check logs for provider initialization errors
4. Test with `await provider.IsAvailableAsync()`

### Provider Returns Errors

**Cause:** API key invalid, rate limits, or network issues

**Solution:**
1. Verify API key is correct
2. Check provider-specific rate limits
3. Increase `TimeoutSeconds` if needed
4. Enable fallback providers

### SwarmUI Not Connecting

**Cause:** SwarmUI not running or wrong URL

**Solution:**
1. Ensure SwarmUI is running: `http://localhost:7801`
2. Check `SWARMUI_URL` environment variable
3. Verify firewall/network settings
4. Test SwarmUI API manually

## 📊 Monitoring & Logging

The system logs important events:

```
[Information] Registered LLM provider: OpenAI (openai)
[Information] Using LLM provider: OpenAI
[Warning] Using fallback LLM provider: OpenAI
[Error] No available LLM providers found
```

Track these logs to monitor:
- Provider availability
- Fallback occurrences
- API failures
- Response times

## 🔮 Future Enhancements

Planned features:
- [ ] Anthropic Claude provider
- [ ] Google Gemini provider
- [ ] Cohere provider
- [ ] Local LLM support (Ollama, LM Studio)
- [ ] Stability AI provider
- [ ] Response caching
- [ ] Rate limiting per provider
- [ ] Cost tracking
- [ ] Streaming support
- [ ] Batch processing
- [ ] A/B testing between providers

## 📝 License

This AI Provider System is part of BuzzFreed and follows the same license.
