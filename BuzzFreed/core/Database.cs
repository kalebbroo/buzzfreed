using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;

namespace BuzzFreed
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string databasePath)
        {
            _connectionString = $"Data Source={databasePath}";
        }

        public async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var tableCommand = "CREATE TABLE IF NOT EXISTS UserQuizzes (" +
                                "UserId TEXT NOT NULL," +
                                "DiscordGuildId TEXT NOT NULL," +
                                "QuizName TEXT NOT NULL," +
                                "QuizResult TEXT," +
                                "QuizImages TEXT," +
                                "Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP," +
                                "PRIMARY KEY(UserId, DiscordGuildId, QuizName));";

            var command = new SqliteCommand(tableCommand, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task AddOrUpdateQuizResultAsync(string userId, string guildId, string quizName, string quizResult, string quizImages)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var insertOrUpdateCommand = "INSERT OR REPLACE INTO UserQuizzes (UserId, DiscordGuildId, QuizName, QuizResult, QuizImages) " +
                                        "VALUES (@UserId, @DiscordGuildId, @QuizName, @QuizResult, @QuizImages);";

            var command = new SqliteCommand(insertOrUpdateCommand, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@DiscordGuildId", guildId);
            command.Parameters.AddWithValue("@QuizName", quizName);
            command.Parameters.AddWithValue("@QuizResult", quizResult);
            command.Parameters.AddWithValue("@QuizImages", quizImages);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<string> GetQuizResultAsync(string userId, string guildId, string quizName)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var selectCommand = "SELECT QuizResult FROM UserQuizzes WHERE UserId = @UserId AND DiscordGuildId = @DiscordGuildId AND QuizName = @QuizName;";

            var command = new SqliteCommand(selectCommand, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@DiscordGuildId", guildId);
            command.Parameters.AddWithValue("@QuizName", quizName);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }
    }
}
