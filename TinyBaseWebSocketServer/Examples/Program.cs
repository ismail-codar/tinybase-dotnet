using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using TinyBaseWebSocketServer.Extensions;
using TinyBaseWebSocketServer.Models.Configuration;
using TinyBaseWebSocketServer.Services;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddWebSocketServer<ExamplePersister>(
    async pathId =>
    {
        // This would return a real persister instance
        return new ExamplePersister(pathId);
    },
    options =>
    {
        options.Port = 5000;
        options.BufferSize = 4096;
        options.MaxMessageSize = 64 * 1024;
        options.EnableAutoCleanup = true;
        options.OperationTimeoutSeconds = 30;
        options.MaxConnectionsPerPath = 100;
        options.WebSocketPath = "/ws";
    },
    error => Log.Error(error, "WebSocket server error")
);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseWebSockets();

// WebSocket endpoint
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    
    // Get path and client ID from the request
    var pathId = context.Request.Path.Value?.TrimStart('/') ?? "default";
    var clientId = context.Request.Headers["Sec-WebSocket-Key"].ToString();
    
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var server = context.RequestServices.GetRequiredService<WebSocketServerService<ExamplePersister>>();
    
    try
    {
        logger.LogInformation("New WebSocket connection: {ClientId} on path {PathId}", clientId, pathId);
        
        // Create WebSocket context wrapper
        var webSocketContext = new DefaultWebSocketContext(context, webSocket);
        
        // Handle the connection
        await server.HandleConnectionAsync(webSocketContext, pathId, clientId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error handling WebSocket connection");
    }
    finally
    {
        logger.LogInformation("WebSocket connection closed: {ClientId} on path {PathId}", clientId, pathId);
    }
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

/// <summary>
/// Example persister implementation for demonstration
/// </summary>
public class ExamplePersister
{
    public string PathId { get; }
    
    public ExamplePersister(string pathId)
    {
        PathId = pathId;
    }
    
    // This would contain the actual persister implementation logic
}

/// <summary>
/// Default implementation of WebSocketContext for demonstration
/// </summary>
public class DefaultWebSocketContext : WebSocketContext
{
    private readonly HttpContext _httpContext;
    private readonly WebSocket _webSocket;
    
    public DefaultWebSocketContext(HttpContext httpContext, WebSocket webSocket)
    {
        _httpContext = httpContext;
        _webSocket = webSocket;
    }
    
    public override WebSocket WebSocket => _webSocket;
    public override HttpContext HttpContext => _httpContext;
    public override bool IsAuthenticated => true;
    public override bool IsLocal => false;
    public override string RequestUri => _httpContext.Request.GetDisplayUrl();
    public override IDictionary<string, object> Items => new Dictionary<string, object>();
}