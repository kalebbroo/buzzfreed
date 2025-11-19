using BuzzFreed.Web.Services;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(); // Use Newtonsoft.Json for better compatibility

// Register custom services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<OpenAIService>();
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

// Initialize database on startup
using (var scope = app.Services.CreateScope())
{
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
