using BuzzFreed.Web.Services;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.AI.Providers.OpenAI;
using BuzzFreed.Web.AI.Providers.SwarmUI;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(); // Use Newtonsoft.Json for better compatibility

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

// Add CORS for Discord iframe
builder.Services.AddCors(options =>
{
    options.AddPolicy("DiscordEmbedded", policy =>
    {
        policy.WithOrigins("https://discord.com", "https://discordapp.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Initialize AI Provider Registry and register providers
using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<AIProviderRegistry>();
    var openaiLLM = scope.ServiceProvider.GetRequiredService<OpenAILLMProvider>();
    var openaiImage = scope.ServiceProvider.GetRequiredService<OpenAIImageProvider>();
    var swarmUI = scope.ServiceProvider.GetRequiredService<SwarmUIImageProvider>();

    // Register all providers
    registry.RegisterLLMProvider(openaiLLM);
    registry.RegisterImageProvider(openaiImage);
    registry.RegisterImageProvider(swarmUI);

    app.Logger.LogInformation("AI Provider Registry initialized");

    // Initialize database
    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
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

// Serve index.html as default document
app.MapFallbackToFile("index.html");

app.Logger.LogInformation("BuzzFreed Discord Activity starting...");
app.Logger.LogInformation($"Environment: {app.Environment.EnvironmentName}");

app.Run();
