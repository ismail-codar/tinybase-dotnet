namespace TinyBaseWebSocketServer.Models.Server;

/// <summary>
/// Represents the state of a WebSocket server client
/// </summary>
public enum WebSocketServerState
{
    /// <summary>
    /// Server client is ready to be configured
    /// </summary>
    Ready = 0,
    
    /// <summary>
    /// Server client is configured and waiting to start
    /// </summary>
    Configured = 1,
    
    /// <summary>
    /// Server client is in the process of starting
    /// </summary>
    Starting = 2
}