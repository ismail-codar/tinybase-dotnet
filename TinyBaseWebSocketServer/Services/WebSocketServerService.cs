namespace TinyBaseWebSocketServer.Services;

/// <summary>
/// Main WebSocket server service that handles connections, message routing, and lifecycle management
/// </summary>
/// <typeparam name="TPersister">The type of persister to use</typeparam>
public class WebSocketServerService<TPersister> : IAsyncDisposable where TPersister : class
{
    private readonly IWebSocketFactory _webSocketFactory;
    private readonly ServerClientFactory<TPersister> _serverClientFactory;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly MessageHandler _messageHandler;
    private readonly WebSocketServerOptions _options;
    private readonly ILogger<WebSocketServerService<TPersister>> _logger;
    
    private WebSocketServer? _webSocketServer;
    private readonly List<string> _pathIdListeners = new();
    private readonly Dictionary<string, List<string>> _clientIdListeners = new();
    private readonly object _listenersLock = new();
    
    /// <summary>
    /// Event raised when the WebSocket server starts
    /// </summary>
    public event EventHandler? ServerStarted;
    
    /// <summary>
    /// Event raised when the WebSocket server stops
    /// </summary>
    public event EventHandler? ServerStopped;
    
    /// <summary>
    /// Event raised when a path becomes active or inactive
    /// </summary>
    public event EventHandler<WebSocketServerEventArgs>? PathChanged;
    
    /// <summary>
    /// Event raised when a client connects or disconnects
    /// </summary>
    public event EventHandler<ClientConnectionEventArgs>? ClientChanged;
    
    public WebSocketServerService(
        IWebSocketFactory webSocketFactory,
        ServerClientFactory<TPersister> serverClientFactory,
        WebSocketConnectionManager connectionManager,
        MessageHandler messageHandler,
        IOptions<WebSocketServerOptions> options,
        ILogger<WebSocketServerService<TPersister>> logger)
    {
        _webSocketFactory = webSocketFactory ?? throw new ArgumentNullException(nameof(webSocketFactory));
        _serverClientFactory = serverClientFactory ?? throw new ArgumentNullException(nameof(serverClientFactory));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Wire up events
        _connectionManager.PathChanged += OnPathChanged;
        _connectionManager.ClientConnected += OnClientChanged;
        _connectionManager.ClientDisconnected += OnClientChanged;
    }
    
    /// <summary>
    /// Starts the WebSocket server
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting WebSocket server on port {Port}", _options.Port);
            
            // Create the WebSocket server
            _webSocketServer = _webSocketFactory.CreateServer(_options.Port);
            
            // Set up connection handling
            _webSocketServer.ConfigureAwait(false);
            
            _logger.LogInformation("WebSocket server started successfully");
            OnServerStarted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WebSocket server");
            throw;
        }
    }
    
    /// <summary>
    /// Stops the WebSocket server
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Stopping WebSocket server");
            
            // Stop all server clients
            await _serverClientFactory.StopAllAsync(cancellationToken);
            
            // Clear all connections
            _connectionManager.ClearAll();
            
            // Close the WebSocket server
            _webSocketServer?.Dispose();
            _webSocketServer = null;
            
            _logger.LogInformation("WebSocket server stopped successfully");
            OnServerStopped();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping WebSocket server");
            throw;
        }
    }
    
    /// <summary>
    /// Handles a new WebSocket connection
    /// </summary>
    /// <param name="context">The WebSocket context</param>
    /// <param name="pathId">The path ID from the URL</param>
    /// <param name="clientId">The client ID from WebSocket headers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task HandleConnectionAsync(WebSocketContext context, string pathId, string clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("New WebSocket connection from client {ClientId} on path {PathId}", clientId, pathId);
            
            // Create a WebSocket connection wrapper
            var connection = new WebSocketConnection(clientId, context.WebSocket, pathId);
            
            // Add the connection
            if (!_connectionManager.AddConnection(pathId, clientId, connection))
            {
                _logger.LogWarning("Client {ClientId} already connected on path {PathId}", clientId, pathId);
                return;
            }
            
            // Get or create server client for this path
            var serverClient = await _serverClientFactory.GetOrCreateServerClientAsync(pathId, cancellationToken);
            if (serverClient == null)
            {
                _logger.LogError("Failed to create server client for path {PathId}", pathId);
                await connection.WebSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Failed to create server client",
                    cancellationToken
                );
                return;
            }
            
            // Configure the server client if it's the first client for this path
            if (_connectionManager.GetClientIds(pathId).Count == 1)
            {
                await ConfigureServerClientAsync(serverClient, pathId, cancellationToken);
            }
            
            // Set up message handling
            await HandleConnectionMessagesAsync(context, connection, serverClient, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection for client {ClientId} on path {PathId}", clientId, pathId);
            
            try
            {
                await context.WebSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Server error",
                    cancellationToken
                );
            }
            catch (Exception closeEx)
            {
                _logger.LogWarning(closeEx, "Failed to close WebSocket connection cleanly");
            }
        }
        finally
        {
            // Clean up connection
            _connectionManager.RemoveConnection(pathId, clientId);
            
            // Stop server client if no more clients for this path
            if (!_connectionManager.HasConnections(pathId))
            {
                await _serverClientFactory.StopServerClientAsync(pathId, null, cancellationToken);
            }
        }
    }
    
    /// <summary>
    /// Gets the current statistics of the WebSocket server
    /// </summary>
    public WebSocketServerStats GetStats()
    {
        return _connectionManager.GetStats();
    }
    
    /// <summary>
    /// Gets all active path IDs
    /// </summary>
    public IReadOnlyList<string> GetPathIds()
    {
        return _connectionManager.GetPathIds();
    }
    
    /// <summary>
    /// Gets all client IDs for a specific path
    /// </summary>
    /// <param name="pathId">The path ID</param>
    public IReadOnlyList<string> GetClientIds(string pathId)
    {
        return _connectionManager.GetClientIds(pathId);
    }
    
    /// <summary>
    /// Adds a listener for path changes
    /// </summary>
    /// <param name="listener">The listener to add</param>
    /// <returns>The listener ID</returns>
    public string AddPathIdsListener(EventHandler<WebSocketServerEventArgs> listener)
    {
        var listenerId = Guid.NewGuid().ToString();
        lock (_listenersLock)
        {
            _pathIdListeners.Add(listenerId);
        }
        
        return listenerId;
    }
    
    /// <summary>
    /// Adds a listener for client changes on a specific path
    /// </summary>
    /// <param name="pathId">The path ID (null for all paths)</param>
    /// <param name="listener">The listener to add</param>
    /// <returns>The listener ID</returns>
    public string AddClientIdsListener(string? pathId, EventHandler<ClientConnectionEventArgs> listener)
    {
        var listenerId = Guid.NewGuid().ToString();
        lock (_listenersLock)
        {
            var key = pathId ?? "*";
            if (!_clientIdListeners.ContainsKey(key))
            {
                _clientIdListeners[key] = new List<string>();
            }
            _clientIdListeners[key].Add(listenerId);
        }
        
        return listenerId;
    }
    
    /// <summary>
    /// Removes a listener
    /// </summary>
    /// <param name="listenerId">The listener ID to remove</param>
    public void RemoveListener(string listenerId)
    {
        lock (_listenersLock)
        {
            _pathIdListeners.Remove(listenerId);
            
            foreach (var kvp in _clientIdListeners.ToList())
            {
                kvp.Value.Remove(listenerId);
                if (kvp.Value.Count == 0)
                {
                    _clientIdListeners.Remove(kvp.Key);
                }
            }
        }
    }
    
    /// <summary>
    /// Sends a message to a specific client
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="clientId">The target client ID</param>
    /// <param name="payload">The message payload</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SendToClientAsync(string pathId, string clientId, string payload)
    {
        await _messageHandler.SendToClientAsync(clientId, pathId, payload);
    }
    
    /// <summary>
    /// Broadcasts a message to all clients in a path
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="payload">The message payload</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task BroadcastToPathAsync(string pathId, string payload)
    {
        var connections = _connectionManager.GetConnections(pathId);
        foreach (var kvp in connections)
        {
            await _messageHandler.SendToClientAsync(kvp.Key, pathId, payload);
        }
    }
    
    private async Task ConfigureServerClientAsync(ServerClient<TPersister> serverClient, string pathId, CancellationToken cancellationToken)
    {
        try
        {
            // Set up send function for the server client
            var sendFunction = new Func<string, Task>(async payload =>
            {
                var messagePayload = MessagePayload.Deserialize(payload);
                if (messagePayload.IsBroadcast)
                {
                    await _messageHandler.BroadcastToPathAsync("S", pathId, payload);
                }
                else if (messagePayload.IsServerMessage)
                {
                    // Send back to server (self-loop)
                    await _messageHandler.SendToClientAsync("S", pathId, payload);
                }
                else
                {
                    await _messageHandler.SendToClientAsync(messagePayload.ToClientId, pathId, payload);
                }
            });
            
            await _serverClientFactory.ConfigureServerClientAsync(
                pathId,
                serverClient,
                sendFunction,
                null, // onStoreLoad callback - would be set based on persister type
                cancellationToken
            );
            
            // Start the server client
            await _serverClientFactory.StartServerClientAsync(pathId, serverClient, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure server client for path {PathId}", pathId);
            throw;
        }
    }
    
    private async Task HandleConnectionMessagesAsync(
        WebSocketContext context,
        WebSocketConnection connection,
        ServerClient<TPersister> serverClient,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.BufferSize];
        
        try
        {
            while (context.WebSocket.State == WebSocketState.Open)
            {
                var result = await context.WebSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken
                );
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    if (serverClient.IsReady)
                    {
                        await _messageHandler.HandleMessageAsync(connection.ClientId, connection.PathId, message);
                    }
                    else
                    {
                        _messageHandler.BufferMessage(connection.PathId, connection.ClientId, message);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Connection was cancelled - normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling WebSocket messages for client {ClientId}", connection.ClientId);
        }
        finally
        {
            try
            {
                await context.WebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed",
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close WebSocket connection cleanly for client {ClientId}", connection.ClientId);
            }
        }
    }
    
    private void OnPathChanged(object? sender, WebSocketServerEventArgs e)
    {
        lock (_listenersLock)
        {
            foreach (var listenerId in _pathIdListeners)
            {
                // In a real implementation, you would invoke the actual listeners
                // For now, we'll just log
                _logger.LogDebug("Path changed: {PathId} - {EventData}", e.PathId, e.Data);
            }
        }
        
        PathChanged?.Invoke(this, e);
    }
    
    private void OnClientChanged(object? sender, ClientConnectionEventArgs e)
    {
        lock (_listenersLock)
        {
            var key = e.PathId;
            if (_clientIdListeners.ContainsKey(key) || _clientIdListeners.ContainsKey("*"))
            {
                foreach (var listenerId in _clientIdListeners.GetValueOrDefault(key, new List<string>()))
                {
                    foreach (var wildcardListenerId in _clientIdListeners.GetValueOrDefault("*", new List<string>()))
                    {
                        // In a real implementation, you would invoke the actual listeners
                        _logger.LogDebug("Client changed: {PathId} - {ClientId}", e.PathId, e.ClientId);
                    }
                }
            }
        }
        
        ClientChanged?.Invoke(this, e);
    }
    
    private void OnServerStarted()
    {
        ServerStarted?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnServerStopped()
    {
        ServerStopped?.Invoke(this, EventArgs.Empty);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_webSocketServer != null)
        {
            await StopAsync();
        }
    }
}

/// <summary>
/// Interface for creating WebSocket servers
/// </summary>
public interface IWebSocketFactory
{
    /// <summary>
    /// Creates a WebSocket server listening on the specified port
    /// </summary>
    /// <param name="port">The port to listen on</param>
    /// <returns>The WebSocket server</returns>
    WebSocketServer CreateServer(int port);
}

/// <summary>
/// Implementation of IWebSocketFactory
/// </summary>
public class WebSocketFactory : IWebSocketFactory
{
    private readonly ILogger<WebSocketFactory> _logger;
    
    public WebSocketFactory(ILogger<WebSocketFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public WebSocketServer CreateServer(int port)
    {
        // In a real implementation, you would create an actual WebSocket server
        // For this example, we'll create a mock server
        _logger.LogInformation("Creating WebSocket server on port {Port}", port);
        
        // This would be replaced with actual ASP.NET Core WebSocket server creation
        return new MockWebSocketServer(port);
    }
}

/// <summary>
/// Mock WebSocket server for demonstration purposes
/// </summary>
public class MockWebSocketServer : IDisposable
{
    public int Port { get; }
    
    public MockWebSocketServer(int port)
    {
        Port = port;
    }
    
    public void Dispose()
    {
        // Mock implementation
    }
}