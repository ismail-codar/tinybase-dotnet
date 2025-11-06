namespace TinyBaseWebSocketServer.Models.Server;

/// <summary>
/// Statistics for the WebSocket server
/// </summary>
public class WebSocketServerStats
{
    /// <summary>
    /// Gets the number of active paths
    /// </summary>
    public int Paths { get; init; }
    
    /// <summary>
    /// Gets the total number of active clients
    /// </summary>
    public int Clients { get; init; }
}