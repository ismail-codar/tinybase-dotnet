namespace TinyBaseWebSocketServer.Models.Events;

/// <summary>
/// Event arguments for WebSocket server events
/// </summary>
public class WebSocketServerEventArgs : EventArgs
{
    /// <summary>
    /// Gets the path ID associated with the event
    /// </summary>
    public string PathId { get; }
    
    /// <summary>
    /// Gets additional event data
    /// </summary>
    public object? Data { get; }

    public WebSocketServerEventArgs(string pathId, object? data = null)
    {
        PathId = pathId ?? throw new ArgumentNullException(nameof(pathId));
        Data = data;
    }
}

/// <summary>
/// Event arguments for client connection events
/// </summary>
public class ClientConnectionEventArgs : EventArgs
{
    /// <summary>
    /// Gets the path ID
    /// </summary>
    public string PathId { get; }
    
    /// <summary>
    /// Gets the client ID
    /// </summary>
    public string ClientId { get; }
    
    /// <summary>
    /// Gets the WebSocket connection
    /// </summary>
    public WebSocketConnection Connection { get; }

    public ClientConnectionEventArgs(string pathId, string clientId, WebSocketConnection connection)
    {
        PathId = pathId ?? throw new ArgumentNullException(nameof(pathId));
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }
}

/// <summary>
/// Event arguments for message events
/// </summary>
public class MessageEventArgs : EventArgs
{
    /// <summary>
    /// Gets the client ID that sent the message
    /// </summary>
    public string ClientId { get; }
    
    /// <summary>
    /// Gets the path ID
    /// </summary>
    public string PathId { get; }
    
    /// <summary>
    /// Gets the message payload
    /// </summary>
    public string Payload { get; }
    
    /// <summary>
    /// Gets the timestamp when the message was received
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    public MessageEventArgs(string clientId, string pathId, string payload)
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        PathId = pathId ?? throw new ArgumentNullException(nameof(pathId));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        Timestamp = DateTimeOffset.UtcNow;
    }
}