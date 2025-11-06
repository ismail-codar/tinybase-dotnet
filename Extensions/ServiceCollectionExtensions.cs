using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using TinyBasePostgresPersister.Configuration;
using TinyBasePostgresPersister.Data.Contexts;
using TinyBasePostgresPersister.Data.Repositories;
using TinyBasePostgresPersister.Services;
using TinyBasePostgresPersister.Services.Persisters;

namespace TinyBasePostgresPersister.Extensions;

/// <summary>
/// Extension methods for dependency injection setup
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add PostgreSQL persister services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="optionsAction">Additional configuration options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddPostgresPersister(
        this IServiceCollection services, 
        string connectionString,
        Action<PostgresPersisterOptions>? optionsAction = null)
    {
        // Configure options
        var options = new PostgresPersisterOptions { ConnectionString = connectionString };
        optionsAction?.Invoke(options);
        services.AddSingleton(options);

        // Add Entity Framework Core with PostgreSQL
        services.AddDbContext<TinyBaseDbContext>(builder =>
        {
            builder.UseNpgsql(options.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(options.CommandTimeout);
            });

            if (options.EnableSqlLogging)
            {
                builder.EnableSensitiveDataLogging();
                builder.LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        // Add repositories
        services.AddScoped<IRepository<Models.Entities.Store>, Repository<Models.Entities.Store>>();
        services.AddScoped<IRepository<Models.Entities.Table>, Repository<Models.Entities.Table>>();
        services.AddScoped<IRepository<Models.Entities.Cell>, Repository<Models.Entities.Cell>>();

        // Add specific repositories
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<ICellRepository, CellRepository>();

        // Add Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Add persister services
        services.AddScoped<IPostgresPersisterFactory, PostgresPersisterFactory>();
        services.AddScoped<IPostgresPersister, PostgresPersister>();

        return services;
    }

    /// <summary>
    /// Add PostgreSQL persister services using IConfiguration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="sectionName">Configuration section name</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddPostgresPersister(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "PostgresPersister")
    {
        var connectionString = configuration.GetConnectionString("Postgres") 
            ?? configuration.GetSection(sectionName)["ConnectionString"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("PostgreSQL connection string not found in configuration");
        }

        var options = new PostgresPersisterOptions();
        configuration.GetSection(sectionName).Bind(options);
        options.ConnectionString = connectionString;

        services.AddSingleton(options);
        services.AddDbContext<TinyBaseDbContext>(builder =>
        {
            builder.UseNpgsql(options.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(options.CommandTimeout);
            });

            if (options.EnableSqlLogging)
            {
                builder.EnableSensitiveDataLogging();
                builder.LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        // Add repositories and services
        services.AddScoped<IRepository<Models.Entities.Store>, Repository<Models.Entities.Store>>();
        services.AddScoped<IRepository<Models.Entities.Table>, Repository<Models.Entities.Table>>();
        services.AddScoped<IRepository<Models.Entities.Cell>, Repository<Models.Entities.Cell>>();

        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<ICellRepository, CellRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPostgresPersisterFactory, PostgresPersisterFactory>();
        services.AddScoped<IPostgresPersister, PostgresPersister>();

        return services;
    }

    /// <summary>
    /// Configure Serilog for the application
    /// </summary>
    /// <param name="builder">Logging builder</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Logging builder</returns>
    public static LoggerConfiguration ConfigureTinyBaseLogging(
        this LoggerConfiguration builder, 
        IConfiguration configuration)
    {
        builder
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .WriteTo.Console()
            .WriteTo.File("logs/tinybase-.txt", 
                rollingInterval: RollingInterval.Day, 
                retainedFileCountLimit: 7);

        if (configuration.GetValue<bool>("PostgresPersister:EnableSqlLogging"))
        {
            builder.MinimumLevel.Override("TinyBasePostgresPersister", LogEventLevel.Debug);
        }

        return builder;
    }
}