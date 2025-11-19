using Microsoft.Data.Sqlite;
using BuzzFreed.Web.Models;
using Newtonsoft.Json;

namespace BuzzFreed.Web.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
        {
            var dbPath = configuration["DatabasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "database", "buzzfreed.db");

            // Ensure directory exists
            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            _connectionString = $"Data Source={dbPath}";
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var tableCommand = @"CREATE TABLE IF NOT EXISTS UserQuizzes (
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

                var command = new SqliteCommand(tableCommand, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                throw;
            }
        }

        public async Task SaveQuizResultAsync(QuizResult result)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var insertCommand = @"INSERT OR REPLACE INTO UserQuizzes
                    (UserId, DiscordGuildId, QuizId, QuizTopic, UserAnswers, ResultPersonality, ResultDescription, Timestamp)
                    VALUES (@UserId, @DiscordGuildId, @QuizId, @QuizTopic, @UserAnswers, @ResultPersonality, @ResultDescription, @Timestamp);";

                var command = new SqliteCommand(insertCommand, connection);
                command.Parameters.AddWithValue("@UserId", result.UserId);
                command.Parameters.AddWithValue("@DiscordGuildId", result.DiscordGuildId);
                command.Parameters.AddWithValue("@QuizId", result.QuizId);
                command.Parameters.AddWithValue("@QuizTopic", result.QuizTopic);
                command.Parameters.AddWithValue("@UserAnswers", JsonConvert.SerializeObject(result.UserAnswers));
                command.Parameters.AddWithValue("@ResultPersonality", result.ResultPersonality);
                command.Parameters.AddWithValue("@ResultDescription", result.ResultDescription);
                command.Parameters.AddWithValue("@Timestamp", result.Timestamp);

                await command.ExecuteNonQueryAsync();
                _logger.LogInformation($"Quiz result saved for user {result.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving quiz result");
                throw;
            }
        }

        public async Task<List<QuizResult>> GetUserQuizHistoryAsync(string userId, string guildId)
        {
            var results = new List<QuizResult>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var selectCommand = @"SELECT * FROM UserQuizzes
                    WHERE UserId = @UserId AND DiscordGuildId = @DiscordGuildId
                    ORDER BY Timestamp DESC;";

                var command = new SqliteCommand(selectCommand, connection);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@DiscordGuildId", guildId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new QuizResult
                    {
                        UserId = reader.GetString(0),
                        DiscordGuildId = reader.GetString(1),
                        QuizId = reader.GetString(2),
                        QuizTopic = reader.GetString(3),
                        UserAnswers = JsonConvert.DeserializeObject<List<string>>(reader.GetString(4)) ?? new List<string>(),
                        ResultPersonality = reader.GetString(5),
                        ResultDescription = reader.GetString(6),
                        Timestamp = reader.GetDateTime(7)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quiz history");
                throw;
            }

            return results;
        }

        public async Task<QuizResult?> GetQuizResultAsync(string userId, string guildId, string quizId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var selectCommand = @"SELECT * FROM UserQuizzes
                    WHERE UserId = @UserId AND DiscordGuildId = @DiscordGuildId AND QuizId = @QuizId;";

                var command = new SqliteCommand(selectCommand, connection);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@DiscordGuildId", guildId);
                command.Parameters.AddWithValue("@QuizId", quizId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new QuizResult
                    {
                        UserId = reader.GetString(0),
                        DiscordGuildId = reader.GetString(1),
                        QuizId = reader.GetString(2),
                        QuizTopic = reader.GetString(3),
                        UserAnswers = JsonConvert.DeserializeObject<List<string>>(reader.GetString(4)) ?? new List<string>(),
                        ResultPersonality = reader.GetString(5),
                        ResultDescription = reader.GetString(6),
                        Timestamp = reader.GetDateTime(7)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quiz result");
                throw;
            }

            return null;
        }
    }
}
