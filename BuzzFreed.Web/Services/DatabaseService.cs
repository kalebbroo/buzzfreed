using Microsoft.Data.Sqlite;
using BuzzFreed.Web.Models;
using BuzzFreed.Web.Utils;
using Newtonsoft.Json;

namespace BuzzFreed.Web.Services;

public class DatabaseService(IConfiguration configuration)
{
    public readonly string ConnectionString = InitializeConnectionString(configuration);

    public static string InitializeConnectionString(IConfiguration configuration)
    {
        string dbPath = configuration["DatabasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "database", "buzzfreed.db");

        // Ensure directory exists
        string? dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        return $"Data Source={dbPath}";
    }

    public async Task InitializeDatabaseAsync()
    {
        try
        {
            using SqliteConnection connection = new(ConnectionString);
            await connection.OpenAsync();

            string tableCommand = @"CREATE TABLE IF NOT EXISTS UserQuizzes (
                UserId TEXT NOT NULL,
                DiscordGuildId TEXT NOT NULL,
                QuizId TEXT NOT NULL,
                QuizTopic TEXT NOT NULL,
                UserAnswers TEXT NOT NULL,
                ResultPersonality TEXT NOT NULL,
                ResultDescription TEXT NOT NULL,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY(UserId, DiscordGuildId, QuizId)
            );";

            SqliteCommand command = new(tableCommand, connection);
            await command.ExecuteNonQueryAsync();

            Logs.Init("Database initialized successfully");
        }
        catch (Exception ex)
        {
            Logs.Error($"Error initializing database: {ex.Message}");
            throw;
        }
    }

    public async Task SaveQuizResultAsync(QuizResult result)
    {
        try
        {
            using SqliteConnection connection = new(ConnectionString);
            await connection.OpenAsync();

            string insertCommand = @"INSERT OR REPLACE INTO UserQuizzes
                (UserId, DiscordGuildId, QuizId, QuizTopic, UserAnswers, ResultPersonality, ResultDescription, Timestamp)
                VALUES (@UserId, @DiscordGuildId, @QuizId, @QuizTopic, @UserAnswers, @ResultPersonality, @ResultDescription, @Timestamp);";

            SqliteCommand command = new(insertCommand, connection);
            command.Parameters.AddWithValue("@UserId", result.UserId);
            command.Parameters.AddWithValue("@DiscordGuildId", result.DiscordGuildId);
            command.Parameters.AddWithValue("@QuizId", result.QuizId);
            command.Parameters.AddWithValue("@QuizTopic", result.QuizTopic);
            command.Parameters.AddWithValue("@UserAnswers", JsonConvert.SerializeObject(result.UserAnswers));
            command.Parameters.AddWithValue("@ResultPersonality", result.ResultPersonality);
            command.Parameters.AddWithValue("@ResultDescription", result.ResultDescription);
            command.Parameters.AddWithValue("@Timestamp", result.Timestamp);

            await command.ExecuteNonQueryAsync();
            Logs.Info($"Quiz result saved for user {result.UserId}");
        }
        catch (Exception ex)
        {
            Logs.Error($"Error saving quiz result: {ex.Message}");
            throw;
        }
    }

    public async Task<List<QuizResult>> GetUserQuizHistoryAsync(string userId, string guildId)
    {
        List<QuizResult> results = new();

        try
        {
            using SqliteConnection connection = new(ConnectionString);
            await connection.OpenAsync();

            string selectCommand = @"SELECT * FROM UserQuizzes
                WHERE UserId = @UserId AND DiscordGuildId = @DiscordGuildId
                ORDER BY Timestamp DESC;";

            SqliteCommand command = new(selectCommand, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@DiscordGuildId", guildId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                List<string>? deserializedAnswers = JsonConvert.DeserializeObject<List<string>>(reader.GetString(4));
                results.Add(new QuizResult
                {
                    UserId = reader.GetString(0),
                    DiscordGuildId = reader.GetString(1),
                    QuizId = reader.GetString(2),
                    QuizTopic = reader.GetString(3),
                    UserAnswers = deserializedAnswers ?? new List<string>(),
                    ResultPersonality = reader.GetString(5),
                    ResultDescription = reader.GetString(6),
                    Timestamp = reader.GetDateTime(7)
                });
            }
        }
        catch (Exception ex)
        {
            Logs.Error($"Error retrieving quiz history: {ex.Message}");
            throw;
        }

        return results;
    }

    public async Task<QuizResult?> GetQuizResultAsync(string userId, string guildId, string quizId)
    {
        try
        {
            using SqliteConnection connection = new(ConnectionString);
            await connection.OpenAsync();

            string selectCommand = @"SELECT * FROM UserQuizzes
                WHERE UserId = @UserId AND DiscordGuildId = @DiscordGuildId AND QuizId = @QuizId;";

            SqliteCommand command = new(selectCommand, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@DiscordGuildId", guildId);
            command.Parameters.AddWithValue("@QuizId", quizId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                List<string>? deserializedAnswers = JsonConvert.DeserializeObject<List<string>>(reader.GetString(4));
                return new QuizResult
                {
                    UserId = reader.GetString(0),
                    DiscordGuildId = reader.GetString(1),
                    QuizId = reader.GetString(2),
                    QuizTopic = reader.GetString(3),
                    UserAnswers = deserializedAnswers ?? new List<string>(),
                    ResultPersonality = reader.GetString(5),
                    ResultDescription = reader.GetString(6),
                    Timestamp = reader.GetDateTime(7)
                };
            }
        }
        catch (Exception ex)
        {
            Logs.Error($"Error retrieving quiz result: {ex.Message}");
            throw;
        }

        return null;
    }
}
