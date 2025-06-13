using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Backend.CMS.Infrastructure.Data
{
    public interface IDatabaseInitializer
    {
        Task EnsureDatabaseExistsAsync(string connectionString, string databaseName);
        Task EnsureHangfireDatabaseExistsAsync();
    }

    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(IConfiguration configuration, ILogger<DatabaseInitializer> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnsureDatabaseExistsAsync(string connectionString, string databaseName)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                var masterConnectionString = connectionString.Replace($"Database={builder.Database}", "Database=postgres");

                using var connection = new NpgsqlConnection(masterConnectionString);
                await connection.OpenAsync();

                // Check if database exists
                var checkCommand = new NpgsqlCommand(
                    "SELECT 1 FROM pg_database WHERE datname = @databaseName", connection);
                checkCommand.Parameters.AddWithValue("databaseName", databaseName);

                var exists = await checkCommand.ExecuteScalarAsync();

                if (exists == null)
                {
                    // Create database
                    var createCommand = new NpgsqlCommand(
                        $"CREATE DATABASE \"{databaseName}\"", connection);
                    await createCommand.ExecuteNonQueryAsync();

                    _logger.LogInformation("Successfully created database: {DatabaseName}", databaseName);
                }
                else
                {
                    _logger.LogInformation("Database already exists: {DatabaseName}", databaseName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure database exists: {DatabaseName}", databaseName);
                throw;
            }
        }

        public async Task EnsureHangfireDatabaseExistsAsync()
        {
            var hangfireConnectionString = _configuration.GetConnectionString("HangfireConnection");

            if (string.IsNullOrEmpty(hangfireConnectionString))
            {
                _logger.LogInformation("Using default connection for Hangfire");
                return;
            }

            var builder = new NpgsqlConnectionStringBuilder(hangfireConnectionString);
            await EnsureDatabaseExistsAsync(hangfireConnectionString, builder.Database ?? "backend_cms_hangfire");
        }
    }
}