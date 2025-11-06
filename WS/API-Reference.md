# API Reference

Complete API reference for the TinyBase WebSocket Server C# implementation.

## WebSocketServerService<TPersister>

Main service for managing WebSocket connections, message routing, and server lifecycle.

### Constructors

```csharp
public WebSocketServerService<TPersister>(
    IWebSocketFactory webSocketFactory,
    ServerClientFactory<TPersister> serverClientFactory,
    WebSocketConnectionManager connectionManager,
    MessageHandler messageHandler,
    IOptions<WebSocketServerOptions> options,
    ILogger<WebSocketServerService<TPersister>> logger)
    where TPersister : class
```

#### Parameters
- `webSocketFactory` - Factory for creating WebSocket servers
- `serverClientFactory` - Factory for creating server clients for paths
- `connectionManager` - Manages WebSocket connections
- `messageHandler` - Handles message routing and delivery
- `options` - Server configuration options
- `logger` - Logger instance for diagnostic information

### Properties

```csharp
// Event properties
public event EventHandler? ServerStarted;
public event EventHandler? ServerStopped;
public event EventHandler<WebSocketServerEventArgs>? PathChanged;
public event EventHandler<ClientConnectionEventArgs>? ClientChanged;
```

### Methods

#### Lifecycle Management

##### `StartAsync`
```csharp
public async Task StartAsync(CancellationToken cancellationToken = default)
```

Starts the WebSocket server and begins accepting connections.

**Parameters:**
- `cancellationToken` - Optional cancellation token

**Exceptions:**
- `InvalidOperationException` - If server is already started
- `Exception` - If server fails to start

**Example:**
```csharp
var server = serviceProvider.GetRequiredService<WebSocketServerService<MyPersister>>();
await server.StartAsync();
```

##### `StopAsync`
```csharp
public async Task StopAsync(CancellationToken cancellationToken = default)
```

Stops the WebSocket server and cleans up resources.

**Parameters:**
- `cancellationToken` - Optional cancellation token

**Example:**
```csharp
await server.StopAsync();
```

#### Connection Management

##### `HandleConnectionAsync`
```csharp
public async Task HandleConnectionAsync(
    WebSocketContext context, 
    string pathId, 
    string clientId, 
    CancellationToken cancellationToken = default)
```

Handles a new WebSocket connection and sets up message handling.

**Parameters:**
- `context` - The WebSocket context from ASP.NET Core
- `pathId` - The path identifier from the URL
- `clientId` - The unique client identifier
- `cancellationToken` - Optional cancellation token

**Behavior:**
- Creates a WebSocket connection wrapper
- Registers the connection in the connection manager
- Sets up message handling for the connection
- Manages server client lifecycle for the path

**Example:**
```csharp
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) return;
    
    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var pathId = context.Request.Path.Value?.TrimStart('/') ?? "default";
    var clientId = context.Request.Headers["Sec-WebSocket-Key"].ToString();
    
    var contextWrapper = new WebSocketContextWrapper(context, webSocket);
    await server.HandleConnectionAsync(contextWrapper, pathId, clientId);
});
```

#### Message Operations

##### `SendToClientAsync`
```csharp
public async Task SendToClientAsync(string pathId, string clientId, string payload)
```

Sends a message to a specific client.

**Parameters:**
- `pathId` - The path ID containing the client
- `clientId` - The target client ID
- `payload` - The message payload to send

**Returns:**
- `Task` - The asynchronous operation

**Example:**
```csharp
// Send a message to a specific client
await server.SendToClientAsync("store1", "client123", "Hello client!");
```

##### `BroadcastToPathAsync`
```csharp
public async Task BroadcastToPathAsync(string pathId, string payload)
```

Broadcasts a message to all clients in a specific path.

**Parameters:**
- `pathId` - The path ID to broadcast to
- `payload` - The message payload to broadcast

**Example:**
```csharp
// Broadcast to all clients in a store
await server.BroadcastToPathAsync("store1", "System update available");
```

#### Statistics and Monitoring

##### `GetStats`
```csharp
public WebSocketServerStats GetStats()
```

Gets current server statistics.

**Returns:**
- `WebSocketServerStats` - Current server statistics

**Properties:**
- `Paths` - Number of active paths
- `Clients` - Total number of active clients

**Example:**
```csharp
var stats = server.GetStats();
Console.WriteLine($"Active paths: {stats.Paths}, Total clients: {stats.Clients}");
```

##### `GetPathIds`
```csharp
public IReadOnlyList<string> GetPathIds()
```

Gets all active path IDs.

**Returns:**
- `IReadOnlyList<string>` - List of active path IDs

##### `GetClientIds`
```csharp
public IReadOnlyList<string> GetClientIds(string pathId)
```

Gets all client IDs for a specific path.

**Parameters:**
- `pathId` - The path ID to get clients for

**Returns:**
- `IReadOnlyList<string>` - List of client IDs for the path

**Example:**
```csharp
var clients = server.GetClientIds("store1");
foreach (var clientId in clients)
{
    Console.WriteLine($"Client: {clientId}");
}
```

#### Event System

##### `AddPathIdsListener`
```csharp
public string AddPathIdsListener(EventHandler<WebSocketServerEventArgs> listener)
```

Adds a listener for path change events.

**Parameters:**
- `listener` - Event handler to invoke when paths change

**Returns:**
- `string` - Listener ID for removing the listener

**Event Data:**
- `PathId` - The path that was affected
- `Data` - Additional event information

**Example:**
```csharp
var listenerId = server.AddPathIdsListener((sender, e) =>
{
    Console.WriteLine($"Path {e.PathId} changed: {e.Data}");
});
```

##### `AddClientIdsListener`
```csharp
public string AddClientIdsListener(string? pathId, EventHandler<ClientConnectionEventArgs> listener)
```

Adds a listener for client connection events.

**Parameters:**
- `pathId` - Specific path to listen to, or null for all paths
- `listener` - Event handler to invoke when clients connect/disconnect

**Returns:**
- `string` - Listener ID for removing the listener

**Event Data:**
- `PathId` - The path ID
- `ClientId` - The client ID
- `Connection` - The WebSocket connection

**Example:**
```csharp
// Listen to all client events
var allListenerId = server.AddClientIdsListener(null, (sender, e) =>
{
    Console.WriteLine($"Client {e.ClientId} on path {e.PathId} changed state");
});

// Listen to specific path
var storeListenerId = server.AddClientIdsListener("store1", (sender, e) =>
{
    Console.WriteLine($"Client {e.ClientId} connected to store1");
});
```

##### `RemoveListener`
```csharp
public void RemoveListener(string listenerId)
```

Removes a previously added event listener.

**Parameters:**
- `listenerId` - The listener ID returned from Add*Listener methods

**Example:**
```csharp
server.RemoveListener(listenerId);
```

## WebSocketConnection

Represents a WebSocket connection with metadata.

### Properties

```csharp
public string ClientId { get; }          // Unique client identifier
public WebSocket WebSocket { get; }      // The underlying WebSocket
public string PathId { get; }            // Path ID for this connection
public DateTimeOffset ConnectedAt { get; } // Connection timestamp
public string? RemoteEndPoint { get; set; } // Remote endpoint info
public WebSocketServerState State { get; set; } // Connection state
public bool IsActive { get; }            // Whether connection is active
```

### Constructor

```csharp
public WebSocketConnection(string clientId, WebSocket webSocket, string pathId)
```

**Parameters:**
- `clientId` - Unique client identifier
- `webSocket` - The underlying WebSocket
- `pathId` - The path ID for this connection

## ServerClient<TPersister>

Represents a server client for a specific path.

### Properties

```csharp
public WebSocketServerState State { get; internal set; }  // Current state
public TPersister Persister { get; internal set; }        // The persister
public object? Synchronizer { get; internal set; }        // The synchronizer
public Func<string, Task>? SendFunction { get; internal set; } // Send function
public List<string> Buffer { get; internal set; }          // Message buffer
public Action<object>? OnStoreLoad { get; internal set; }  // Store load callback
public bool IsReady { get; }                              // Ready state
public bool IsConfigured { get; }                         // Configured state
public bool IsStarting { get; }                           // Starting state
```

### States

```csharp
public enum WebSocketServerState
{
    Ready = 0,        // Ready to be configured
    Configured = 1,   // Configured, waiting to start
    Starting = 2      // In the process of starting
}
```

## MessagePayload

Handles message creation, parsing, and validation.

### Constructors

```csharp
public MessagePayload(string toClientId, string? requestId, string message, string? body = null)
```

**Parameters:**
- `toClientId` - Target client ID (empty for broadcast, "S" for server)
- `requestId` - Optional request ID
- `message` - The message type
- `body` - Optional message body

### Static Factory Methods

##### `Broadcast`
```csharp
public static MessagePayload Broadcast(string? requestId, string message, string? body = null)
```

Creates a broadcast message (empty target client ID).

##### `ToServer`
```csharp
public static MessagePayload ToServer(string? requestId, string message, string? body = null)
```

Creates a message targeting the server.

##### `ToClient`
```csharp
public static MessagePayload ToClient(string clientId, string? requestId, string message, string? body = null)
```

Creates a message targeting a specific client.

### Methods

##### `Serialize`
```csharp
public string Serialize()
```

Serializes the message payload to a string format.

**Returns:**
- `string` - Serialized message in format: ToClientId|RequestId|Message|Body

##### `Deserialize`
```csharp
public static MessagePayload Deserialize(string payload)
```

Deserializes a message payload from string format.

**Parameters:**
- `payload` - The payload string to deserialize

**Returns:**
- `MessagePayload` - Deserialized message payload

**Exceptions:**
- `ArgumentException` - If the payload format is invalid

### Extension Methods

##### `TryParsePayload`
```csharp
public static bool TryParsePayload(string payload, out MessagePayload? messagePayload, out string? error)
```

Safely parses a payload string with error handling.

**Parameters:**
- `payload` - The payload string to parse
- `messagePayload` - Output parameter for the parsed payload
- `error` - Output parameter for any error message

**Returns:**
- `bool` - True if parsing was successful, false otherwise

##### `CreateRawPayload`
```csharp
public static string CreateRawPayload(string fromClientId, string payload)
```

Creates a raw payload with client ID prefix.

**Parameters:**
- `fromClientId` - The client ID sending the message
- `payload` - The message payload

**Returns:**
- `string` - Raw payload in format: FromClientId|Payload

##### `CreateRoutingPayload`
```csharp
public static string CreateRoutingPayload(string toClientId, string? requestId, string message, string? body = null)
```

Creates a complete routing payload.

**Parameters:**
- `toClientId` - Target client ID
- `requestId` - Optional request ID
- `message` - Message type
- `body` - Optional message body

**Returns:**
- `string` - Complete routing payload

### Properties and Methods

##### `IsServerMessage`
```csharp
public static bool IsServerMessage(this MessagePayload payload)
```

Checks if the message targets the server.

##### `IsBroadcast`
```csharp
public static bool IsBroadcast(this MessagePayload payload)
```

Checks if the message is a broadcast (empty target).

## MessageHandler

Manages message routing and delivery between WebSocket clients.

### Constructors

```csharp
public MessageHandler(WebSocketConnectionManager connectionManager, string serverClientId = "S")
```

**Parameters:**
- `connectionManager` - The connection manager
- `serverClientId` - The server client ID (default: "S")

### Properties

```csharp
public event EventHandler<MessageEventArgs>? MessageReceived;
public event EventHandler<MessageEventArgs>? MessageSent;
public event EventHandler<MessageEventArgs>? MessageSendFailed;
```

### Methods

##### `HandleMessageAsync`
```csharp
public async Task HandleMessageAsync(string fromClientId, string pathId, string payload)
```

Handles a received message and routes it appropriately.

**Parameters:**
- `fromClientId` - The client that sent the message
- `pathId` - The path ID
- `payload` - The message payload

##### `SendToClientAsync`
```csharp
public async Task<bool> SendToClientAsync(string toClientId, string pathId, string payload)
```

Sends a message to a specific client.

**Parameters:**
- `toClientId` - Target client ID
- `pathId` - The path ID
- `payload` - The message payload

**Returns:**
- `bool` - True if the message was sent successfully

##### `BroadcastToPathAsync`
```csharp
public async Task<bool> BroadcastToPathAsync(string fromClientId, string pathId, string payload)
```

Broadcasts a message to all clients in a path (except sender).

**Parameters:**
- `fromClientId` - The client that sent the message
- `pathId` - The path ID
- `payload` - The message payload

**Returns:**
- `bool` - True if at least one message was sent

##### `BufferMessage`
```csharp
public void BufferMessage(string pathId, string clientId, string payload)
```

Buffers a message for a client that is not yet ready.

**Parameters:**
- `pathId` - The path ID
- `clientId` - The client ID
- `payload` - The message payload

##### `GetAndClearBufferedMessages`
```csharp
public IReadOnlyList<string> GetAndClearBufferedMessages(string pathId, string clientId)
```

Retrieves and clears buffered messages for a client.

**Parameters:**
- `pathId` - The path ID
- `clientId` - The client ID

**Returns:**
- `IReadOnlyList<string>` - The buffered messages

## WebSocketConnectionManager

Manages WebSocket connections and provides access to connected clients.

### Constructors

```csharp
public WebSocketConnectionManager()
```

### Properties

```csharp
public event EventHandler<ClientConnectionEventArgs>? ClientConnected;
public event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;
public event EventHandler<WebSocketServerEventArgs>? PathChanged;
```

### Methods

##### `AddConnection`
```csharp
public bool AddConnection(string pathId, string clientId, WebSocketConnection connection)
```

Adds a WebSocket connection for a client.

**Parameters:**
- `pathId` - The path ID
- `clientId` - The client ID
- `connection` - The WebSocket connection

**Returns:**
- `bool` - True if the connection was added, false if it already exists

##### `RemoveConnection`
```csharp
public bool RemoveConnection(string pathId, string clientId)
```

Removes a WebSocket connection for a client.

**Parameters:**
- `pathId` - The path ID
- `clientId` - The client ID

**Returns:**
- `bool` - True if the connection was removed

##### `GetConnection`
```csharp
public WebSocketConnection? GetConnection(string pathId, string clientId)
```

Gets a WebSocket connection for a specific client.

**Parameters:**
- `pathId` - The path ID
- `clientId` - The client ID

**Returns:**
- `WebSocketConnection?` - The connection if found, null otherwise

##### `GetConnections`
```csharp
public IReadOnlyDictionary<string, WebSocketConnection> GetConnections(string pathId)
```

Gets all connections for a specific path.

**Parameters:**
- `pathId` - The path ID

**Returns:**
- `IReadOnlyDictionary<string, WebSocketConnection>` - Dictionary of client IDs to connections

##### `GetClientIds`
```csharp
public IReadOnlyList<string> GetClientIds(string pathId)
```

Gets all client IDs for a specific path.

**Parameters:**
- `pathId` - The path ID

**Returns:**
- `IReadOnlyList<string>` - List of client IDs

##### `GetPathIds`
```csharp
public IReadOnlyList<string> GetPathIds()
```

Gets all path IDs.

**Returns:**
- `IReadOnlyList<string>` - List of path IDs

##### `GetTotalClientCount`
```csharp
public int GetTotalClientCount()
```

Gets the total number of active clients across all paths.

**Returns:**
- `int` - Total client count

##### `GetPathCount`
```csharp
public int GetPathCount()
```

Gets the number of active paths.

**Returns:**
- `int` - Number of active paths

##### `GetStats`
```csharp
public WebSocketServerStats GetStats()
```

Gets statistics for all connections.

**Returns:**
- `WebSocketServerStats` - Connection statistics

##### `HasConnections`
```csharp
public bool HasConnections(string pathId)
```

Checks if a path has any active connections.

**Parameters:**
- `pathId` - The path ID

**Returns:**
- `bool` - True if the path has active connections

##### `ClearAll`
```csharp
public void ClearAll()
```

Clears all connections (used during server shutdown).

## WebSocketServerOptions

Configuration options for the WebSocket server.

### Properties

```csharp
public int Port { get; set; } = 5000                          // Server port
public int MaxMessageSize { get; set; } = 64 * 1024           // Max message size
public int BufferSize { get; set; } = 4096                    // Buffer size
public bool EnableAutoCleanup { get; set; } = true            // Auto cleanup
public int OperationTimeoutSeconds { get; set; } = 30         // Timeout
public int MaxConnectionsPerPath { get; set; } = 100          // Connection limit
public string WebSocketPath { get; set; } = "/ws"             // WebSocket path
```

### Configuration Example

```csharp
services.AddWebSocketServer<MyPersister>(
    persisterFactory,
    options =>
    {
        options.Port = 8080;
        options.BufferSize = 8192;
        options.MaxMessageSize = 128 * 1024; // 128KB
        options.OperationTimeoutSeconds = 60;
        options.MaxConnectionsPerPath = 200;
    }
);
```

## Event Arguments

### WebSocketServerEventArgs

```csharp
public class WebSocketServerEventArgs : EventArgs
{
    public string PathId { get; }        // Path ID
    public object? Data { get; }         // Additional data
}
```

### ClientConnectionEventArgs

```csharp
public class ClientConnectionEventArgs : EventArgs
{
    public string PathId { get; }                    // Path ID
    public string ClientId { get; }                  // Client ID
    public WebSocketConnection Connection { get; }   // Connection
}
```

### MessageEventArgs

```csharp
public class MessageEventArgs : EventArgs
{
    public string ClientId { get; }          // Client ID
    public string PathId { get; }            // Path ID
    public string Payload { get; }           // Message payload
    public DateTimeOffset Timestamp { get; } // Message timestamp
}
```

## Extension Methods

### ServiceCollectionExtensions

```csharp
public static IServiceCollection AddWebSocketServer<TPersister>(
    this IServiceCollection services,
    Func<string, Task<TPersister?>> configurePersister,
    Action<WebSocketServerOptions>? configureOptions = null)
    where TPersister : class
```

Adds the WebSocket server service to the service collection.

**Parameters:**
- `services` - The service collection
- `configurePersister` - Function to create persisters for paths
- `configureOptions` - Optional configuration action

**Returns:**
- `IServiceCollection` - The service collection (for chaining)

### Example Usage

```csharp
builder.Services.AddWebSocketServer<MyPersister>(
    async pathId => new MyPersister(pathId),
    options =>
    {
        options.Port = 5000;
        options.BufferSize = 4096;
        options.MaxMessageSize = 64 * 1024;
    }
);
```

## Constants

### Message Format Constants

```csharp
public static class MessageConstants
{
    public const string ServerClientId = "S";           // Server client ID
    public const char PayloadSeparator = '|';           // Message separator
    public const string EmptyClientId = "";             // Broadcast client ID
}
```

These constants are used throughout the API for consistent message handling and client identification.