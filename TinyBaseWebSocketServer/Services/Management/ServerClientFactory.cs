namespace TinyBaseWebSocketServer.Services.Management;

/// <summary>
/// Factory for creating server clients for different paths
/// </summary>
/// <typeparam name="TPersister">The type of persister to use</typeparam>
public class ServerClientFactory<TPersister> where TPersister : class
{
    private readonly Func<string, Task<TPersister?>> _persisterFactory;
    private readonly Action<object>? _errorHandler;
    private readonly Dictionary<string, ServerClient<TPersister>> _serverClients = new();
    private readonly object _lock = new();
    
    public ServerClientFactory(
        Func<string, Task<TPersister?>> persisterFactory,
        Action<object>? errorHandler = null)
    {
        _persisterFactory = persisterFactory ?? throw new ArgumentNullException(nameof(persisterFactory));
        _errorHandler = errorHandler;
    }
    
    /// <summary>
    /// Gets or creates a server client for a path
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The server client</returns>
    public async Task<ServerClient<TPersister>?> GetOrCreateServerClientAsync(string pathId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            throw new ArgumentException("Path ID cannot be null or empty", nameof(pathId));

        lock (_lock)
        {
            if (_serverClients.TryGetValue(pathId, out var existingClient))
            {
                return existingClient;
            }
        }
        
        // Create a new server client
        var persister = await _persisterFactory(pathId);
        if (persister == null)
        {
            return null;
        }
        
        var serverClient = new ServerClient<TPersister>
        {
            Persister = persister,
            State = WebSocketServerState.Ready
        };
        
        lock (_lock)
        {
            _serverClients[pathId] = serverClient;
        }
        
        return serverClient;
    }
    
    /// <summary>
    /// Configures a server client with its components
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="serverClient">The server client to configure</param>
    /// <param name="sendFunction">The send function to use</param>
    /// <param name="onStoreLoad">Callback when store is loaded</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task ConfigureServerClientAsync(
        string pathId,
        ServerClient<TPersister> serverClient,
        Func<string, Task>? sendFunction = null,
        Action<object>? onStoreLoad = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            throw new ArgumentException("Path ID cannot be null or empty", nameof(pathId));
            
        if (serverClient == null)
            throw new ArgumentNullException(nameof(serverClient));
            
        serverClient.State = WebSocketServerState.Configured;
        serverClient.SendFunction = sendFunction != null ? payload => sendFunction(payload) : null;
        serverClient.OnStoreLoad = onStoreLoad;
        
        // Execute the store load callback if provided
        if (onStoreLoad != null)
        {
            try
            {
                onStoreLoad(serverClient.Persister);
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(ex);
            }
        }
    }
    
    /// <summary>
    /// Starts a server client
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="serverClient">The server client to start</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StartServerClientAsync(string pathId, ServerClient<TPersister> serverClient, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            throw new ArgumentException("Path ID cannot be null or empty", nameof(pathId));
            
        if (serverClient == null)
            throw new ArgumentNullException(nameof(serverClient));

        serverClient.State = WebSocketServerState.Starting;
        
        try
        {
            // In a real implementation, you would start the persister and synchronizer here
            // For now, we'll simulate the startup process
            await Task.Delay(100, cancellationToken); // Simulate startup delay
            
            serverClient.State = WebSocketServerState.Ready;
        }
        catch (Exception ex)
        {
            serverClient.State = WebSocketServerState.Ready; // Reset to ready state on error
            _errorHandler?.Invoke(ex);
            throw;
        }
    }
    
    /// <summary>
    /// Stops a server client
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="serverClient">The server client to stop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StopServerClientAsync(string pathId, ServerClient<TPersister>? serverClient = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            throw new ArgumentException("Path ID cannot be null or empty", nameof(pathId));

        ServerClient<TPersister>? clientToStop = serverClient;
        
        lock (_lock)
        {
            if (clientToStop == null)
            {
                _serverClients.TryGetValue(pathId, out clientToStop);
            }
        }
        
        if (clientToStop != null)
        {
            try
            {
                // In a real implementation, you would stop the persister and synchronizer here
                await Task.Delay(50, cancellationToken); // Simulate shutdown delay
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(ex);
            }
            
            lock (_lock)
            {
                _serverClients.Remove(pathId);
            }
        }
    }
    
    /// <summary>
    /// Gets a server client for a path
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <returns>The server client if found, null otherwise</returns>
    public ServerClient<TPersister>? GetServerClient(string pathId)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            return null;

        lock (_lock)
        {
            return _serverClients.TryGetValue(pathId, out var client) ? client : null;
        }
    }
    
    /// <summary>
    /// Gets all active server client path IDs
    /// </summary>
    /// <returns>A list of path IDs</returns>
    public IReadOnlyList<string> GetActivePathIds()
    {
        lock (_lock)
        {
            return _serverClients.Keys.ToList().AsReadOnly();
        }
    }
    
    /// <summary>
    /// Stops all server clients
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        List<ServerClient<TPersister>> clientsToStop;
        
        lock (_lock)
        {
            clientsToStop = _serverClients.Values.ToList();
        }
        
        foreach (var client in clientsToStop)
        {
            // Find the path ID for this client
            var pathId = _serverClients.FirstOrDefault(kvp => kvp.Value == client).Key;
            if (!string.IsNullOrEmpty(pathId))
            {
                await StopServerClientAsync(pathId, client, cancellationToken);
            }
        }
    }
}