namespace TinyBaseWebSocketServer.Extensions;

/// <summary>
/// Extension methods for configuring WebSocket server services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the WebSocket server service to the service collection
    /// </summary>
    /// <typeparam name="TPersister">The type of persister to use</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configurePersister">Configuration for the persister factory</param>
    /// <param name="configureOptions">Configuration for WebSocket server options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddWebSocketServer<TPersister>(
        this IServiceCollection services,
        Func<string, Task<TPersister?>> configurePersister,
        Action<WebSocketServerOptions>? configureOptions = null)
        where TPersister : class
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure<WebSocketServerOptions>(configureOptions);
        }
        else
        {
            services.Configure<WebSocketServerOptions>(options =>
            {
                options.Port = 5000;
                options.BufferSize = 4096;
                options.MaxMessageSize = 64 * 1024;
                options.EnableAutoCleanup = true;
                options.OperationTimeoutSeconds = 30;
                options.MaxConnectionsPerPath = 100;
                options.WebSocketPath = "/ws";
            });
        }
        
        // Register services
        services.AddSingleton<WebSocketConnectionManager>();
        services.AddSingleton<MessageHandler>();
        services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
        services.AddScoped<ServerClientFactory<TPersister>>();
        services.AddScoped<WebSocketServerService<TPersister>>();
        
        // Register the persister factory
        services.AddSingleton(configurePersister);
        
        return services;
    }
    
    /// <summary>
    /// Adds the WebSocket server service with additional configuration
    /// </summary>
    /// <typeparam name="TPersister">The type of persister to use</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="persisterFactory">The persister factory function</param>
    /// <param name="serverOptions">The WebSocket server options</param>
    /// <param name="errorHandler">Optional error handler</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddWebSocketServer<TPersister>(
        this IServiceCollection services,
        Func<string, Task<TPersister?>> persisterFactory,
        WebSocketServerOptions serverOptions,
        Action<object>? errorHandler = null)
        where TPersister : class
    {
        services.Configure<WebSocketServerOptions>(options =>
        {
            options.Port = serverOptions.Port;
            options.BufferSize = serverOptions.BufferSize;
            options.MaxMessageSize = serverOptions.MaxMessageSize;
            options.EnableAutoCleanup = serverOptions.EnableAutoCleanup;
            options.OperationTimeoutSeconds = serverOptions.OperationTimeoutSeconds;
            options.MaxConnectionsPerPath = serverOptions.MaxConnectionsPerPath;
            options.WebSocketPath = serverOptions.WebSocketPath;
        });
        
        // Register services
        services.AddSingleton<WebSocketConnectionManager>();
        services.AddSingleton<MessageHandler>();
        services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
        services.AddScoped<ServerClientFactory<TPersister>>();
        services.AddScoped<WebSocketServerService<TPersister>>();
        
        // Register the persister factory
        services.AddSingleton(persisterFactory);
        
        // Register error handler
        if (errorHandler != null)
        {
            services.AddSingleton(errorHandler);
        }
        
        return services;
    }
}