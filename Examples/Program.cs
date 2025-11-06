using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using TinyBasePostgresPersister.Configuration;
using TinyBasePostgresPersister.Extensions;
using TinyBasePostgresPersister.Services;
using TinyBasePostgresPersister.Services.Persisters;

namespace TinyBasePostgresPersister.Examples;

/// <summary>
/// Example application demonstrating PostgreSQL persister usage
/// </summary>
public class Program
{
    private static async Task Main(string[] args)
    {
        // Configure logging
        var loggerConfig = new LoggerConfiguration()
            .ConfigureTinyBaseLogging(BuildConfiguration());
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
        var connectionString = "Host=localhost;Database=tinybase;Username=postgres;Password=password;Port=5432";
        
        var services = new ServiceCollection();
        
        // Add PostgreSQL persister services
        services.AddPostgresPersister(connectionString, options =>
        {
            options.AutoCreateSchema = true;
            options.EnableSqlLogging = true;
            options.CommandTimeout = 60;
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
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var persisterFactory = serviceProvider.GetRequiredService<IPostgresPersisterFactory>();

        logger.LogInformation("Starting TinyBase PostgreSQL Persister Example");

        // Example 1: Simple table persistence
        await ExampleSimpleTablePersistence(serviceProvider, logger, persisterFactory);

        // Example 2: JSON configuration persistence
        await ExampleJsonConfigurationPersistence(serviceProvider, logger, persisterFactory);

        // Example 3: Multiple table management
        await ExampleMultipleTableManagement(serviceProvider, logger, persisterFactory);

        logger.LogInformation("Example completed successfully");
    }

    private static async Task ExampleSimpleTablePersistence(
        ServiceProvider serviceProvider, 
        ILogger logger, 
        IPostgresPersisterFactory persisterFactory)
    {
        logger.LogInformation("=== Example 1: Simple Table Persistence ===");

        using var persister = await persisterFactory.CreatePostgresPersisterAsync(
            "example_store_1",
            "simple_table",
            onSqlCommand: (sql, @params) => logger.LogDebug("SQL: {Sql}", sql),
            onIgnoredError: ex => logger.LogWarning("Ignored error: {Error}", ex.Message));

        // Load existing data
        await persister.LoadAsync();
        logger.LogInformation("Loaded data from database");

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
        logger.LogInformation("Started listening for database changes");

        // Simulate some operations (in real usage, these would be from TinyBase)
        await Task.Delay(2000);

        await persister.SaveAsync();
        logger.LogInformation("Saved data to database");

        await persister.StopListeningAsync();
        logger.LogInformation("Stopped listening");
    }

    private static async Task ExampleJsonConfigurationPersistence(
        ServiceProvider serviceProvider,
        ILogger logger,
        IPostgresPersisterFactory persisterFactory)
    {
        logger.LogInformation("=== Example 2: JSON Configuration Persistence ===");

        var jsonConfig = @"
        {
            ""isJson"": true,
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

        using var persister = await persisterFactory.CreatePostgresPersisterAsync(
            "example_store_2",
            jsonConfig,
            onSqlCommand: (sql, @params) => logger.LogDebug("SQL: {Sql}", sql),
            onIgnoredError: ex => logger.LogWarning("Ignored error: {Error}", ex.Message));

        logger.LogInformation("Created persister with JSON configuration");
        logger.LogInformation("Configuration hash: {Hash}", persister.ConfigHash);

        await persister.LoadAsync();
        await persister.StartListeningAsync();

        // Listen for changes
        persister.DataChanged += (sender, args) =>
        {
            logger.LogInformation("Data change detected - Table: {TableName}, Type: {EventType}", 
                args.TableName, args.EventType);
        };

        await Task.Delay(2000);
        await persister.SaveAsync();
        await persister.StopListeningAsync();
    }

    private static async Task ExampleMultipleTableManagement(
        ServiceProvider serviceProvider,
        ILogger logger,
        IPostgresPersisterFactory persisterFactory)
    {
        logger.LogInformation("=== Example 3: Multiple Table Management ===");

        var config = new
        {
            isJson = false,
            managedTableNames = new[] { "customers", "orders", "order_items" }
        };

        var jsonConfig = System.Text.Json.JsonSerializer.Serialize(config);

        using var persister = await persisterFactory.CreatePostgresPersisterAsync(
            "example_store_3",
            jsonConfig);

        logger.LogInformation("Managing {TableCount} tables: {Tables}", 
            config.managedTableNames.Length, 
            string.Join(", ", config.managedTableNames));

        await persister.LoadAsync();
        await persister.StartListeningAsync();

        persister.DataChanged += (sender, args) =>
        {
            logger.LogInformation("Change in {TableName}: {EventType}", 
                args.TableName, args.EventType);
        };

        persister.TableCreated += (sender, args) =>
        {
            logger.LogInformation("New table created: {TableName}", args.TableName);
        };

        // Simulate some business operations
        logger.LogInformation("Simulating business operations...");
        await Task.Delay(3000);

        await persister.SaveAsync();
        logger.LogInformation("All changes saved");

        await persister.StopListeningAsync();
        logger.LogInformation("Multi-table example completed");
    }
}