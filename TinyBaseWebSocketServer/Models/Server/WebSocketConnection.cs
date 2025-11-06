namespace TinyBaseWebSocketServer.Models.Server;

/// <summary>
/// Represents a WebSocket connection with metadata
/// </summary>
public class WebSocketConnection
{
    /// <summary>
    /// Gets the unique client ID for this connection
    /// </summary>
    public string ClientId { get; }
    
    /// <summary>
    /// Gets the WebSocket context
    /// </summary>
    public WebSocket WebSocket { get; }
    
    /// <summary>
    /// Gets the path ID for this connection
    /// </summary>
    public string PathId { get; }
    
    /// <summary>
    /// Gets when the connection was established
    /// </summary>
    public DateTimeOffset ConnectedAt { get; }
    
    /// <summary>
    /// Gets the remote endpoint information
    /// </summary>
    public string? RemoteEndPoint { get; set; }
    
    /// <summary>
    /// Gets or sets the current connection state
    /// </summary>
    public WebSocketServerState State { get; set; } = WebSocketServerState.Ready;
    
    /// <summary>
    /// Gets whether the connection is currently active
    /// </summary>
    public bool IsActive => State != WebSocketServerState.Ready;

    public WebSocketConnection(string clientId, WebSocket webSocket, string pathId)
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        WebSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        PathId = pathId ?? throw new ArgumentNullException(nameof(pathId));
        ConnectedAt = DateTimeOffset.UtcNow;
    }
}