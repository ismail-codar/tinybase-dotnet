using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using TinyBaseSqlitePersister.Configuration;
using TinyBaseSqlitePersister.Extensions;
using TinyBaseSqlitePersister.Services;
using TinyBaseSqlitePersister.Services.Persisters;

namespace TinyBaseSqlitePersister.Examples;

/// <summary>
/// Example application demonstrating SQLite persister usage
/// </summary>
public class SqliteProgram
{
    private static async Task Main(string[] args)
    {
        // Configure logging
        var loggerConfig = new LoggerConfiguration()
            .ConfigureSqliteTinyBaseLogging(BuildConfiguration());
        Log.Logger = loggerConfig.CreateLogger();

        // Build service provider
        var serviceProvider = BuildServiceProvider();

        try
        {
            await RunExampleAsync(serviceProvider);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlushAsync();
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        // Use in-memory SQLite for the example
        var connectionString = "Data Source=:memory:;Mode=Memory;Cache=Shared;";
        
        var services = new ServiceCollection();
        
        // Add SQLite persister services
        services.AddSqlitePersister(connectionString, options =>
        {
            options.AutoLoadIntervalSeconds = 3; // Poll every 3 seconds
            options.EnableSqlLogging = true;
            options.CommandTimeout = 30;
        });

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddSerilog();
        });

        return services.BuildServiceProvider();
    }

    private static async Task RunExampleAsync(ServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<SqliteProgram>>();
        var persisterFactory = serviceProvider.GetRequiredService<ISqlitePersisterFactory>();

        logger.LogInformation("Starting TinyBase SQLite Persister Example");

        // Example 1: Simple table persistence
        await ExampleSimpleTablePersistence(serviceProvider, logger, persisterFactory);

        // Example 2: JSON configuration persistence
        await ExampleJsonConfigurationPersistence(serviceProvider, logger, persisterFactory);

        // Example 3: File-based database
        await ExampleFileBasedDatabase(serviceProvider, logger, persisterFactory);

        logger.LogInformation("SQLite example completed successfully");
    }

    private static async Task ExampleSimpleTablePersistence(
        ServiceProvider serviceProvider, 
        Microsoft.Extensions.Logging.ILogger logger, 
        ISqlitePersisterFactory persisterFactory)
    {
        logger.LogInformation("=== Example 1: Simple Table Persistence ===");

        using var persister = await persisterFactory.CreateSqlitePersisterAsync(
            "sqlite_store_1",
            "simple_table",
            onSqlCommand: (sql, @params) => logger.LogDebug("SQL: {Sql}", sql),
            onIgnoredError: ex => logger.LogWarning("Ignored error: {Error}", ex.Message));

        // Load existing data
        await persister.LoadAsync();
        logger.LogInformation("Loaded data from SQLite database");

        // Set up change tracking
        persister.DataChanged += (sender, args) =>
        {
            logger.LogInformation("Data changed in table: {TableName}, Event: {EventType}", 
                args.TableName, args.EventType);
        };

        persister.TableCreated += (sender, args) =>
        {
            logger.LogInformation("Table created: {TableName}", args.TableName);
        };

        // Start listening for changes
        await persister.StartListeningAsync();
        logger.LogInformation("Started listening for SQLite database changes");

        // Simulate some operations (in real usage, these would be from TinyBase)
        await Task.Delay(3000);

        await persister.SaveAsync();
        logger.LogInformation("Saved data to SQLite database");

        await persister.StopListeningAsync();
        logger.LogInformation("Stopped listening");
    }

    private static async Task ExampleJsonConfigurationPersistence(
        ServiceProvider serviceProvider,
        Microsoft.Extensions.Logging.ILogger logger,
        ISqlitePersisterFactory persisterFactory)
    {
        logger.LogInformation("=== Example 2: JSON Configuration Persistence ===");

        var jsonConfig = @"
        {
            ""isJson"": true,
            ""autoLoadIntervalSeconds"": 5,
            ""managedTableNames"": [""users"", ""products"", ""orders""],
            ""tableConfigs"": [
                {
                    ""tableName"": ""users"",
                    ""columnNames"": [""name"", ""email"", ""age""],
                    ""columnTypes"": [""text"", ""text"", ""integer""]
                },
                {
                    ""tableName"": ""products"",
                    ""columnNames"": [""name"", ""price"", ""category""],
                    ""columnTypes"": [""text"", ""decimal"", ""text""]
                }
            ]
        }";

        using var persister = await persisterFactory.CreateSqlitePersisterAsync(
            "sqlite_store_2",
            jsonConfig,
            onSqlCommand: (sql, @params) => logger.LogDebug("SQL: {Sql}", sql),
            onIgnoredError: ex => logger.LogWarning("Ignored error: {Error}", ex.Message));

        logger.LogInformation("Created SQLite persister with JSON configuration");
        logger.LogInformation("Configuration hash: {Hash}", persister.ConfigHash);

        await persister.LoadAsync();
        await persister.StartListeningAsync();

        // Listen for changes
        persister.DataChanged += (sender, args) =>
        {
            logger.LogInformation("Data change detected - Table: {TableName}, Type: {EventType}", 
                args.TableName, args.EventType);
        };

        await Task.Delay(3000);
        await persister.SaveAsync();
        await persister.StopListeningAsync();
    }

    private static async Task ExampleFileBasedDatabase(
        ServiceProvider serviceProvider,
        Microsoft.Extensions.Logging.ILogger logger,
        ISqlitePersisterFactory persisterFactory)
    {
        logger.LogInformation("=== Example 3: File-Based Database ===");

        var databasePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "tinybase_file.db");
        
        using var persister = await persisterFactory.CreateFileBasedSqlitePersisterAsync(
            "sqlite_store_3",
            databasePath,
            onSqlCommand: (sql, @params) => logger.LogDebug("File DB SQL: {Sql}", sql),
            onIgnoredError: ex => logger.LogWarning("File DB error: {Error}", ex.Message));

        logger.LogInformation("Using file-based SQLite database: {Path}", databasePath);

        await persister.LoadAsync();
        await persister.StartListeningAsync();

        persister.DataChanged += (sender, args) =>
        {
            logger.LogInformation("File DB change in {TableName}: {EventType}", 
                args.TableName, args.EventType);
        };

        // Simulate business operations
        logger.LogInformation("Simulating file database operations...");
        await Task.Delay(3000);

        await persister.SaveAsync();
        logger.LogInformation("File database changes saved");

        await persister.StopListeningAsync();
        logger.LogInformation("File-based database example completed");
        
        // Show file information
        if (File.Exists(databasePath))
        {
            var fileInfo = new FileInfo(databasePath);
            logger.LogInformation("Database file: {Path}, Size: {Size} bytes", 
                databasePath, fileInfo.Length);
        }
    }
}