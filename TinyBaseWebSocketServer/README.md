# TinyBase WebSocket Server for C#

A comprehensive C# implementation of the TinyBase WebSocket server synchronizer, providing real-time WebSocket-based data synchronization for multi-tenant applications.

## Overview

This project recreates the functionality of the TypeScript WebSocket server synchronizer from [TinyBase](https://github.com/tinyplex/tinybase) in C# using modern .NET technologies. It provides a robust, production-ready WebSocket server that handles:

- **Multi-tenant path-based routing** - Different URL paths represent different data stores
- **Real-time message routing** - Messages are forwarded between connected clients
- **Server client lifecycle management** - Automatic configuration, starting, and stopping of per-path data persisters
- **Connection management** - Robust WebSocket connection handling with cleanup
- **Event-driven architecture** - Comprehensive event system for monitoring and integration
- **Statistics and monitoring** - Real-time server statistics and client tracking

## Features

✅ **WebSocket Server Implementation**
- ASP.NET Core WebSocket support
- Path-based multi-tenancy
- Real-time connection management

✅ **Message Routing System**
- Client-to-client message forwarding
- Server message buffering
- Automatic payload parsing and validation

✅ **Server Client Management**
- Automatic persister creation and lifecycle management
- Server state management (Ready, Configured, Starting)
- Graceful startup and shutdown procedures

✅ **Event System**
- Path and client connection events
- Message routing events
- Server lifecycle events

✅ **Statistics and Monitoring**
- Real-time connection statistics
- Path and client tracking
- Server health monitoring

✅ **Integration Ready**
- Dependency injection friendly
- Compatible with Entity Framework Core persisters
- Configurable through options pattern

## Requirements

- .NET 8.0 or later
- ASP.NET Core 8.0 or later
- Entity Framework Core 8.0 or later (for persister integration)

## Installation

1. **Clone or copy the project files** to your solution
2. **Add to your project** as a reference or NuGet package
3. **Configure services** in your startup code

```bash
# If you have the project as a NuGet package
dotnet add package TinyBaseWebSocketServer

# Or reference directly in your .csproj
<ProjectReference Include="path/to/TinyBaseWebSocketServer/TinyBaseWebSocketServer.csproj" />
```

## Quick Start

### Basic Setup

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TinyBaseWebSocketServer.Extensions;
using TinyBaseWebSocketServer.Models.Configuration;

// In your Program.cs or Startup.cs
var builder = WebApplication.CreateBuilder(args);

// Add WebSocket server services
builder.Services.AddWebSocketServer<YourPersisterType>(
    async pathId => 
    {
        // Your persister creation logic
        return new YourPersisterType(pathId);
    },
    options =>
    {
        options.Port = 5000;
        options.BufferSize = 4096;
        options.MaxMessageSize = 64 * 1024;
    }
);

var app = builder.Build();

// Add WebSocket middleware
app.UseWebSockets();

// Configure WebSocket endpoint
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    
    // Extract path and client ID
    var pathId = context.Request.Path.Value?.TrimStart('/') ?? "default";
    var clientId = context.Request.Headers["Sec-WebSocket-Key"].ToString();
    
    // Get server service
    var server = context.RequestServices.GetRequiredService<WebSocketServerService<YourPersisterType>>();
    
    // Create WebSocket context and handle connection
    var webSocketContext = new DefaultWebSocketContext(context, webSocket);
    await server.HandleConnectionAsync(webSocketContext, pathId, clientId);
});

app.Run();
```

### Integration with Entity Framework Persister

```csharp
// Example with PostgreSQL persister from previous implementations
builder.Services.AddWebSocketServer<PostgresPersister>(
    async pathId =>
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var persisterFactory = serviceProvider.GetRequiredService<PostgresPersisterFactory>();
        return await persisterFactory.CreatePersisterAsync(pathId);
    },
    options =>
    {
        options.Port = 8080;
        options.MaxConnectionsPerPath = 50;
        options.OperationTimeoutSeconds = 60;
    }
);
```

## Configuration

### WebSocket Server Options

```csharp
public class WebSocketServerOptions
{
    public int Port { get; set; } = 5000;                    // Server port
    public int MaxMessageSize { get; set; } = 64 * 1024;     // 64KB max message
    public int BufferSize { get; set; } = 4096;              // 4KB buffer
    public bool EnableAutoCleanup { get; set; } = true;      // Auto cleanup
    public int OperationTimeoutSeconds { get; set; } = 30;   // 30s timeout
    public int MaxConnectionsPerPath { get; set; } = 100;    // Max connections
    public string WebSocketPath { get; set; } = "/ws";       // WebSocket path
}
```

### Environment Configuration

```json
{
  "WebSocket": {
    "Port": 5000,
    "BufferSize": 4096,
    "MaxMessageSize": 65536,
    "EnableAutoCleanup": true,
    "OperationTimeoutSeconds": 30,
    "MaxConnectionsPerPath": 100,
    "WebSocketPath": "/ws"
  }
}
```

## API Reference

### WebSocketServerService<TPersister>

The main service for managing WebSocket connections and server lifecycle.

#### Key Methods

```csharp
// Lifecycle management
Task StartAsync(CancellationToken cancellationToken = default);
Task StopAsync(CancellationToken cancellationToken = default);

// Connection handling
Task HandleConnectionAsync(WebSocketContext context, string pathId, string clientId, CancellationToken cancellationToken = default);

// Message operations
Task SendToClientAsync(string pathId, string clientId, string payload);
Task BroadcastToPathAsync(string pathId, string payload);

// Statistics and monitoring
WebSocketServerStats GetStats();
IReadOnlyList<string> GetPathIds();
IReadOnlyList<string> GetClientIds(string pathId);

// Event system
string AddPathIdsListener(EventHandler<WebSocketServerEventArgs> listener);
string AddClientIdsListener(string? pathId, EventHandler<ClientConnectionEventArgs> listener);
void RemoveListener(string listenerId);
```

### MessagePayload

Handles message creation, parsing, and routing.

```csharp
// Create different types of messages
MessagePayload.Broadcast(requestId, message, body);
MessagePayload.ToServer(requestId, message, body);
MessagePayload.ToClient(clientId, requestId, message, body);

// Serialize/deserialize
string payload = messagePayload.Serialize();
MessagePayload parsed = MessagePayload.Deserialize(payload);

// Validate and parse
if (MessagePayloadExtensions.TryParsePayload(payload, out var messagePayload, out var error))
{
    // Use parsed message
}
```

### MessageHandler

Manages message routing and delivery.

```csharp
// Handle received messages
await messageHandler.HandleMessageAsync(fromClientId, pathId, payload);

// Send to specific client
await messageHandler.SendToClientAsync(clientId, pathId, payload);

// Broadcast to path
await messageHandler.BroadcastToPathAsync(fromClientId, pathId, payload);

// Buffer messages for offline clients
messageHandler.BufferMessage(pathId, clientId, payload);
```

## Message Format

The WebSocket server uses a pipe-separated message format:

```
ToClientId|RequestId|MessageType|Body
```

### Message Types

- **Broadcast** - Empty ToClientId: `|req123|update|{data}`
- **To Server** - "S" as ToClientId: `S|req123|query|{query}`
- **To Client** - Specific client ID: `client123|req123|response|{data}`

### Example Messages

```javascript
// Client connects and sends a message
const ws = new WebSocket('ws://localhost:5000/ws/my-store');
ws.onopen = () => {
    // Send a broadcast message
    ws.send('|req001|broadcast|Hello everyone!');
    
    // Send a message to server
    ws.send('S|req002|query|getData');
    
    // Send a message to another client
    ws.send('otherClient|req003|direct|Private message');
};
```

## Event System

### Available Events

```csharp
// Server lifecycle events
server.ServerStarted += (sender, e) => { /* Handle server start */ };
server.ServerStopped += (sender, e) => { /* Handle server stop */ };

// Path management events
server.PathChanged += (sender, e) => { 
    Console.WriteLine($"Path changed: {e.PathId} - {e.Data}"); 
};

// Client connection events
server.ClientChanged += (sender, e) => { 
    Console.WriteLine($"Client {e.ClientId} {e.Connection.WebSocket.State} on path {e.PathId}"); 
};

// Message events
messageHandler.MessageReceived += (sender, e) => { 
    Console.WriteLine($"Message from {e.ClientId}: {e.Payload}"); 
};
```

## Integration Examples

### With PostgreSQL Persister

```csharp
// Configure with PostgreSQL persister
builder.Services.AddWebSocketServer<PostgresPersister>(
    async pathId =>
    {
        var optionsBuilder = new DbContextOptionsBuilder<TinyBaseDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        
        using var context = new TinyBaseDbContext(optionsBuilder.Options);
        return new PostgresPersister(context, pathId);
    },
    options =>
    {
        options.Port = 5432;
        options.MaxConnectionsPerPath = 200;
    }
);
```

### With SQLite Persister

```csharp
// Configure with SQLite persister
builder.Services.AddWebSocketServer<SqlitePersister>(
    async pathId =>
    {
        var connectionString = $"Data Source={pathId}.db";
        var optionsBuilder = new DbContextOptionsBuilder<SqliteDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        
        using var context = new SqliteDbContext(optionsBuilder.Options);
        return new SqlitePersister(context, pathId);
    }
);
```

### Custom Persister

```csharp
public class MyCustomPersister
{
    public string PathId { get; }
    public MergeableStore Store { get; }
    
    public MyCustomPersister(string pathId)
    {
        PathId = pathId;
        Store = new MergeableStore();
    }
    
    // Implement persister interface methods...
}

// Configure with custom persister
builder.Services.AddWebSocketServer<MyCustomPersister>(
    async pathId => new MyCustomPersister(pathId)
);
```

## Security Considerations

1. **Authentication**: Implement proper authentication before accepting WebSocket connections
2. **Authorization**: Validate path access based on user permissions
3. **Rate Limiting**: Implement rate limiting to prevent abuse
4. **Message Validation**: Validate all incoming messages before processing
5. **Connection Limits**: Configure appropriate connection limits per path

```csharp
// Example security middleware
app.Use(async (context, next) =>
{
    if (context.WebSockets.IsWebSocketRequest && context.Request.Path.StartsWithSegments("/ws"))
    {
        // Validate authentication token
        var token = context.Request.Query["token"].ToString();
        if (!ValidateToken(token))
        {
            context.Response.StatusCode = 401;
            return;
        }
        
        // Extract and validate path
        var pathId = context.Request.Path.Value?.TrimStart('/');
        if (!IsAuthorized(token, pathId))
        {
            context.Response.StatusCode = 403;
            return;
        }
    }
    
    await next();
});
```

## Performance Optimization

1. **Connection Pooling**: Reuse database connections where possible
2. **Message Batching**: Batch multiple small messages for better performance
3. **Buffer Management**: Tune buffer sizes based on your message patterns
4. **Connection Limits**: Set appropriate connection limits to prevent resource exhaustion

## Troubleshooting

### Common Issues

1. **WebSocket Connection Fails**
   - Check that `UseWebSockets()` is called before route configuration
   - Verify the WebSocket path matches client connection
   - Ensure CORS is properly configured

2. **Messages Not Routing**
   - Check message format matches expected pipe-separated format
   - Verify client IDs are properly assigned
   - Check server logs for routing errors

3. **High Memory Usage**
   - Tune buffer sizes in options
   - Implement message cleanup for offline clients
   - Monitor connection counts and implement cleanup

### Logging

Enable detailed logging for troubleshooting:

```csharp
// In appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "TinyBaseWebSocketServer": "Debug"
    }
  }
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## Support

For issues and questions:
- Create an issue in the repository
- Check the documentation and examples
- Review the TypeScript implementation for reference

## Related Projects

- [TinyBase PostgreSQL Persister](link-to-postgres-implementation)
- [TinyBase SQLite Persister](link-to-sqlite-implementation)
- [TinyBase Official Repository](https://github.com/tinyplex/tinybase)