using BuzzFreed.Web.Services;
using BuzzFreed.Web.Services.Multiplayer;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.AI.Providers.OpenAI;
using BuzzFreed.Web.AI.Providers.SwarmUI;
using BuzzFreed.Web.Hubs;
using BuzzFreed.Web.Models.Multiplayer.GameModes;
using BuzzFreed.Web.Utils;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// Initialize Logs
Logs.LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "buzzfreed.log");
Directory.CreateDirectory(Path.GetDirectoryName(Logs.LogFilePath)!);

// Load environment variables from .env file
Env.Load();

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(); // Use Newtonsoft.Json for better compatibility

// Add SignalR for real-time multiplayer communication
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 64 * 1024; // 64KB max message size
});

// Register AI Provider Registry (singleton)
builder.Services.AddSingleton<AIProviderRegistry>();

// Register AI Providers
builder.Services.AddSingleton<OpenAILLMProvider>();
builder.Services.AddSingleton<OpenAIImageProvider>();
builder.Services.AddSingleton<SwarmUIImageProvider>();

// Register custom services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<OpenAIService>(); // Kept for backward compatibility
builder.Services.AddSingleton<QuizService>();

// Register Multiplayer services
builder.Services.AddSingleton<GameModeRegistry>();
builder.Services.AddSingleton<TimerService>();
builder.Services.AddSingleton<BroadcastService>();
builder.Services.AddSingleton<GameSessionService>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<InteractionService>();

// Add CORS for Discord iframe (with SignalR support)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DiscordEmbedded", policy =>
    {
        policy.WithOrigins(
                "https://discord.com",
                "https://discordapp.com",
                "https://ptb.discord.com",
                "https://canary.discord.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

WebApplication app = builder.Build();

// Initialize AI Provider Registry and register providers
using (IServiceScope scope = app.Services.CreateScope())
{
    AIProviderRegistry registry = scope.ServiceProvider.GetRequiredService<AIProviderRegistry>();
    OpenAILLMProvider openaiLLM = scope.ServiceProvider.GetRequiredService<OpenAILLMProvider>();
    OpenAIImageProvider openaiImage = scope.ServiceProvider.GetRequiredService<OpenAIImageProvider>();
    SwarmUIImageProvider swarmUI = scope.ServiceProvider.GetRequiredService<SwarmUIImageProvider>();

    // Register all providers
    registry.RegisterLLMProvider(openaiLLM);
    registry.RegisterImageProvider(openaiImage);
    registry.RegisterImageProvider(swarmUI);

    Logs.Init("AI Provider Registry initialized");

    // Initialize database
    DatabaseService dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    await dbService.InitializeDatabaseAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Enable CORS
app.UseCors("DiscordEmbedded");

// Serve static files from wwwroot
app.UseStaticFiles();

// Map API controllers
app.MapControllers();

// Map SignalR hub for real-time game communication
app.MapHub<GameHub>("/hubs/game");

// Initialize Game Mode Registry
using (IServiceScope scope = app.Services.CreateScope())
{
    GameModeRegistry gameModeRegistry = scope.ServiceProvider.GetRequiredService<GameModeRegistry>();
    gameModeRegistry.RegisterMode(new HotSeatMode());
    gameModeRegistry.RegisterMode(new TeamChallengeMode());
    Logs.Init("Game Mode Registry initialized");
}

// Serve index.html as default document
app.MapFallbackToFile("index.html");

Logs.Init("BuzzFreed Discord Activity starting...");
Logs.Info($"Environment: {app.Environment.EnvironmentName}");
Logs.Info("SignalR hub available at /hubs/game");

app.Run();
