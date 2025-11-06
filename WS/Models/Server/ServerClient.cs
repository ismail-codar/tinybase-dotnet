namespace TinyBaseWebSocketServer.Models.Server;

/// <summary>
/// Represents a server client for a specific path
/// </summary>
/// <typeparam name="TPersister">The type of persister to use</typeparam>
public class ServerClient<TPersister> where TPersister : class
{
    /// <summary>
    /// Gets the current state of the server client
    /// </summary>
    public WebSocketServerState State { get; internal set; } = WebSocketServerState.Ready;
    
    /// <summary>
    /// Gets the persister for this server client
    /// </summary>
    public TPersister Persister { get; internal set; } = null!;
    
    /// <summary>
    /// Gets the synchronizer for this server client
    /// </summary>
    public object? Synchronizer { get; internal set; }
    
    /// <summary>
    /// Gets or sets the send function for this server client
    /// </summary>
    public Func<string, Task>? SendFunction { get; internal set; }
    
    /// <summary>
    /// Gets the buffer for messages received before configuration
    /// </summary>
    public List<string> Buffer { get; internal set; } = new();
    
    /// <summary>
    /// Gets or sets the callback to execute when the store is loaded
    /// </summary>
    public Action<object>? OnStoreLoad { get; internal set; }
    
    /// <summary>
    /// Gets whether the server client is ready to process messages
    /// </summary>
    public bool IsReady => State == WebSocketServerState.Ready;
    
    /// <summary>
    /// Gets whether the server client is configured
    /// </summary>
    public bool IsConfigured => State == WebSocketServerState.Configured;
    
    /// <summary>
    /// Gets whether the server client is starting
    /// </summary>
    public bool IsStarting => State == WebSocketServerState.Starting;
}