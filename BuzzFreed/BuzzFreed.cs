using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DotNetEnv;
using BuzzFreed;
using Discord.Commands;
using Newtonsoft.Json;

class Program
{
    static async Task Main(string[] args)
    {
        // Load .env file
        Env.Load();

        // Set environment variables
        Environment.SetEnvironmentVariable("PATH", Env.GetString("PATH"));

        // Initialization
        DiscordSocketClient _client = new DiscordSocketClient();

        // Construct database path
        string dbDirectory = Path.Combine(Directory.GetCurrentDirectory(), "database");
        string dbFile = "buzzfreed.db";
        string dbPath = Path.Combine(dbDirectory, dbFile);

        // Ensure directory exists
        Directory.CreateDirectory(dbDirectory);

        // Initialize the DatabaseService
        DatabaseService _databaseService = new DatabaseService(dbPath);

        // Event handling
        _client.Log += Log;

        // Add the Ready event handler and pass the DiscordSocketClient and DatabaseService
        _client.Ready += async () => await OnReady(_databaseService, _client);

        // Fetch tokens from environment variables
        string discordToken = Env.GetString("BOT_TOKEN");
        string gptApiKey = Env.GetString("GPT_KEY");

        // Login and start the bot
        await _client.LoginAsync(TokenType.Bot, discordToken);
        await _client.StartAsync();

        // Keep the program running
        await Task.Delay(-1);
    }

    static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    static async Task OnReady(DatabaseService _databaseService, DiscordSocketClient _client)
    {
        // Initialize database tables
        await _databaseService.InitializeDatabaseAsync();

        // Register slash commands
        foreach (var guild in _client.Guilds)
        {
            var guildCommand = new SlashCommandBuilder()
                .WithName("quiz")
                .WithDescription("Start a new quiz!");

            try
            {
                await guild.CreateApplicationCommandAsync(guildCommand.Build());
            }
            catch (Exception exception)
            {
                // In this case, I'm using a generic Exception to catch any issues
                Console.WriteLine($"An error occurred: {exception.Message}");
            }
        }
        // Write in console that the bot is ready using name of bot
        Console.WriteLine($"{_client.CurrentUser} is connected!");
        // Write in console how many slash commands the bot has registered
        Console.WriteLine($"Registered {_client.Guilds.Count} slash commands");
        
        // You can add other "ready" related tasks here.
    }

}
