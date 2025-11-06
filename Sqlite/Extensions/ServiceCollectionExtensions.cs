using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using TinyBaseSqlitePersister.Configuration;
using TinyBaseSqlitePersister.Data.Contexts;
using TinyBaseSqlitePersister.Data.Repositories;
using TinyBaseSqlitePersister.Services;
using TinyBaseSqlitePersister.Services.Persisters;

namespace TinyBaseSqlitePersister.Extensions;

/// <summary>
/// Extension methods for dependency injection setup for SQLite
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add SQLite persister services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQLite connection string</param>
    /// <param name="optionsAction">Additional configuration options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddSqlitePersister(
        this IServiceCollection services, 
        string connectionString,
        Action<SqlitePersisterOptions>? optionsAction = null)
    {
        // Configure options
        var options = new SqlitePersisterOptions { ConnectionString = connectionString };
        optionsAction?.Invoke(options);
        services.AddSingleton(options);

        // Add Entity Framework Core with SQLite
        services.AddDbContext<SqliteDbContext>(builder =>
        {
            builder.UseSqlite(options.ConnectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(options.CommandTimeout);
            });

            if (options.EnableSqlLogging)
            {
                builder.EnableSensitiveDataLogging();
                builder.LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        // Add repositories
        services.AddScoped<IRepository<Models.Entities.SqliteStore>, Repository<Models.Entities.SqliteStore>>();
        services.AddScoped<IRepository<Models.Entities.SqliteTable>, Repository<Models.Entities.SqliteTable>>();
        services.AddScoped<IRepository<Models.Entities.SqliteCell>, Repository<Models.Entities.SqliteCell>>();

        // Add specific repositories
        services.AddScoped<ISqliteStoreRepository, SqliteStoreRepository>();
        services.AddScoped<ISqliteTableRepository, SqliteTableRepository>();
        services.AddScoped<ISqliteCellRepository, SqliteCellRepository>();

        // Add Unit of Work
        services.AddScoped<ISqliteUnitOfWork, SqliteUnitOfWork>();

        // Add persister services
        services.AddScoped<ISqlitePersisterFactory, SqlitePersisterFactory>();
        services.AddScoped<ISqlitePersister, SqlitePersister>();

        return services;
    }

    /// <summary>
    /// Add SQLite persister services using IConfiguration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="sectionName">Configuration section name</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddSqlitePersister(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "SqlitePersister")
    {
        var connectionString = configuration.GetConnectionString("Sqlite") 
            ?? configuration.GetSection(sectionName)["ConnectionString"]
            ?? "Data Source=tinybase.db;Mode=ReadWriteCreate;Cache=Private;";

        var options = new SqlitePersisterOptions();
        configuration.GetSection(sectionName).Bind(options);
        options.ConnectionString = connectionString;

        services.AddSingleton(options);
        services.AddDbContext<SqliteDbContext>(builder =>
        {
            builder.UseSqlite(options.ConnectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(options.CommandTimeout);
            });

            if (options.EnableSqlLogging)
            {
                builder.EnableSensitiveDataLogging();
                builder.LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        // Add repositories and services
        services.AddScoped<IRepository<Models.Entities.SqliteStore>, Repository<Models.Entities.SqliteStore>>();
        services.AddScoped<IRepository<Models.Entities.SqliteTable>, Repository<Models.Entities.SqliteTable>>();
        services.AddScoped<IRepository<Models.Entities.SqliteCell>, Repository<Models.Entities.SqliteCell>>();

        services.AddScoped<ISqliteStoreRepository, SqliteStoreRepository>();
        services.AddScoped<ISqliteTableRepository, SqliteTableRepository>();
        services.AddScoped<ISqliteCellRepository, SqliteCellRepository>();

        services.AddScoped<ISqliteUnitOfWork, SqliteUnitOfWork>();
        services.AddScoped<ISqlitePersisterFactory, SqlitePersisterFactory>();
        services.AddScoped<ISqlitePersister, SqlitePersister>();

        return services;
    }

    /// <summary>
    /// Configure Serilog for the SQLite application
    /// </summary>
    /// <param name="builder">Logging builder</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Logging builder</returns>
    public static LoggerConfiguration ConfigureSqliteTinyBaseLogging(
        this LoggerConfiguration builder, 
        IConfiguration configuration)
    {
        builder
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .WriteTo.Console()
            .WriteTo.File("logs/tinybase-sqlite-.txt", 
                rollingInterval: RollingInterval.Day, 
                retainedFileCountLimit: 7);

        if (configuration.GetValue<bool>("SqlitePersister:EnableSqlLogging"))
        {
            builder.MinimumLevel.Override("TinyBaseSqlitePersister", LogEventLevel.Debug);
        }

        return builder;
    }

    /// <summary>
    /// Add in-memory SQLite database (for testing)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="databaseName">Database name</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddInMemorySqlitePersister(
        this IServiceCollection services, 
        string databaseName = "TestDatabase")
    {
        var connectionString = $"Data Source=:memory:;Cache=Private;";
        
        services.AddSingleton(provider => new SqlitePersisterOptions
        {
            ConnectionString = connectionString,
            UseInMemory = true,
            DatabaseFilePath = null
        });

        services.AddDbContext<SqliteDbContext>(builder =>
        {
            builder.UseSqlite(connectionString);
        });

        // Add repositories and services (same as other configurations)
        services.AddScoped<IRepository<Models.Entities.SqliteStore>, Repository<Models.Entities.SqliteStore>>();
        services.AddScoped<IRepository<Models.Entities.SqliteTable>, Repository<Models.Entities.SqliteTable>>();
        services.AddScoped<IRepository<Models.Entities.SqliteCell>, Repository<Models.Entities.SqliteCell>>();

        services.AddScoped<ISqliteStoreRepository, SqliteStoreRepository>();
        services.AddScoped<ISqliteTableRepository, SqliteTableRepository>();
        services.AddScoped<ISqliteCellRepository, SqliteCellRepository>();

        services.AddScoped<ISqliteUnitOfWork, SqliteUnitOfWork>();
        services.AddScoped<ISqlitePersisterFactory, SqlitePersisterFactory>();
        services.AddScoped<ISqlitePersister, SqlitePersister>();

        return services;
    }
}