using TinyBaseWebSocketServer.Models.Events;
using TinyBaseWebSocketServer.Models.Server;

namespace TinyBaseWebSocketServer.Services.Management;

/// <summary>
/// Manages WebSocket connections and provides access to connected clients
/// </summary>
public class WebSocketConnectionManager
{
    private readonly Dictionary<string, Dictionary<string, WebSocketConnection>> _clientsByPath = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Event raised when a client connects
    /// </summary>
    public event EventHandler<ClientConnectionEventArgs>? ClientConnected;
    
    /// <summary>
    /// Event raised when a client disconnects
    /// </summary>
    public event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;
    
    /// <summary>
    /// Event raised when a path becomes active or inactive
    /// </summary>
    public event EventHandler<WebSocketServerEventArgs>? PathChanged;
    
    /// <summary>
    /// Adds a WebSocket connection for a client
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="connection">The WebSocket connection</param>
    /// <returns>True if the connection was added, false if already exists</returns>
    public bool AddConnection(string pathId, string clientId, WebSocketConnection connection)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            throw new ArgumentException("Path ID cannot be null or empty", nameof(pathId));
            
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));
            
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        lock (_lock)
        {
            // Get or create the path dictionary
            if (!_clientsByPath.TryGetValue(pathId, out var pathClients))
            {
                pathClients = new Dictionary<string, WebSocketConnection>();
                _clientsByPath[pathId] = pathClients;
            }
            
            // Check if client already exists
            if (pathClients.ContainsKey(clientId))
            {
                return false;
            }
            
            // Add the connection
            pathClients[clientId] = connection;
            
            // If this is the first client for this path, raise PathChanged event
            if (pathClients.Count == 1)
            {
                OnPathChanged(new WebSocketServerEventArgs(pathId, "Path activated"));
            }
            
            // Raise ClientConnected event
            OnClientConnected(new ClientConnectionEventArgs(pathId, clientId, connection));
            
            return true;
        }
    }
    
    /// <summary>
    /// Removes a WebSocket connection for a client
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="clientId">The client ID</param>
    /// <returns>True if the connection was removed, false if not found</returns>
    public bool RemoveConnection(string pathId, string clientId)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            throw new ArgumentException("Path ID cannot be null or empty", nameof(pathId));
            
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        lock (_lock)
        {
            if (!_clientsByPath.TryGetValue(pathId, out var pathClients))
            {
                return false;
            }
            
            if (!pathClients.TryGetValue(clientId, out var connection))
            {
                return false;
            }
            
            // Remove the connection
            pathClients.Remove(clientId);
            
            // Remove the path if no clients remain
            if (pathClients.Count == 0)
            {
                _clientsByPath.Remove(pathId);
                OnPathChanged(new WebSocketServerEventArgs(pathId, "Path deactivated"));
            }
            
            // Raise ClientDisconnected event
            OnClientDisconnected(new ClientConnectionEventArgs(pathId, clientId, connection));
            
            return true;
        }
    }
    
    /// <summary>
    /// Gets a WebSocket connection for a specific client
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="clientId">The client ID</param>
    /// <returns>The WebSocketConnection if found, null otherwise</returns>
    public WebSocketConnection? GetConnection(string pathId, string clientId)
    {
        if (string.IsNullOrWhiteSpace(pathId) || string.IsNullOrWhiteSpace(clientId))
            return null;

        lock (_lock)
        {
            if (_clientsByPath.TryGetValue(pathId, out var pathClients) &&
                pathClients.TryGetValue(clientId, out var connection))
            {
                return connection;
            }
            return null;
        }
    }
    
    /// <summary>
    /// Gets all connections for a specific path
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <returns>A dictionary of client IDs to connections</returns>
    public IReadOnlyDictionary<string, WebSocketConnection> GetConnections(string pathId)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            return new Dictionary<string, WebSocketConnection>();

        lock (_lock)
        {
            if (_clientsByPath.TryGetValue(pathId, out var pathClients))
            {
                return new Dictionary<string, WebSocketConnection>(pathClients);
            }
            return new Dictionary<string, WebSocketConnection>();
        }
    }
    
    /// <summary>
    /// Gets all client IDs for a specific path
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <returns>A list of client IDs</returns>
    public IReadOnlyList<string> GetClientIds(string pathId)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            return new List<string>();

        lock (_lock)
        {
            if (_clientsByPath.TryGetValue(pathId, out var pathClients))
            {
                return pathClients.Keys.ToList().AsReadOnly();
            }
            return new List<string>();
        }
    }
    
    /// <summary>
    /// Gets all path IDs
    /// </summary>
    /// <returns>A list of path IDs</returns>
    public IReadOnlyList<string> GetPathIds()
    {
        lock (_lock)
        {
            return _clientsByPath.Keys.ToList().AsReadOnly();
        }
    }
    
    /// <summary>
    /// Gets the total number of active clients across all paths
    /// </summary>
    public int GetTotalClientCount()
    {
        lock (_lock)
        {
            return _clientsByPath.Sum(kvp => kvp.Value.Count);
        }
    }
    
    /// <summary>
    /// Gets the number of active paths
    /// </summary>
    public int GetPathCount()
    {
        lock (_lock)
        {
            return _clientsByPath.Count;
        }
    }
    
    /// <summary>
    /// Gets statistics for all connections
    /// </summary>
    public WebSocketServerStats GetStats()
    {
        lock (_lock)
        {
            return new WebSocketServerStats
            {
                Paths = _clientsByPath.Count,
                Clients = _clientsByPath.Sum(kvp => kvp.Value.Count)
            };
        }
    }
    
    /// <summary>
    /// Checks if a path has any active connections
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <returns>True if the path has active connections</returns>
    public bool HasConnections(string pathId)
    {
        if (string.IsNullOrWhiteSpace(pathId))
            return false;

        lock (_lock)
        {
            return _clientsByPath.TryGetValue(pathId, out var pathClients) && pathClients.Count > 0;
        }
    }
    
    /// <summary>
    /// Clears all connections
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _clientsByPath.Clear();
        }
    }
    
    protected virtual void OnClientConnected(ClientConnectionEventArgs e)
    {
        ClientConnected?.Invoke(this, e);
    }
    
    protected virtual void OnClientDisconnected(ClientConnectionEventArgs e)
    {
        ClientDisconnected?.Invoke(this, e);
    }
    
    protected virtual void OnPathChanged(WebSocketServerEventArgs e)
    {
        PathChanged?.Invoke(this, e);
    }
}