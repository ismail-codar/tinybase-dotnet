namespace TinyBaseWebSocketServer.Models.Configuration;

/// <summary>
/// Configuration options for the WebSocket server
/// </summary>
public class WebSocketServerOptions
{
    /// <summary>
    /// Gets or sets the default port for the WebSocket server
    /// </summary>
    public int Port { get; set; } = 5000;
    
    /// <summary>
    /// Gets or sets the maximum message size in bytes
    /// </summary>
    public int MaxMessageSize { get; set; } = 64 * 1024; // 64KB
    
    /// <summary>
    /// Gets or sets the buffer size for WebSocket operations
    /// </summary>
    public int BufferSize { get; set; } = 4096; // 4KB
    
    /// <summary>
    /// Gets or sets whether to enable automatic connection cleanup
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the timeout for WebSocket operations in seconds
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets the maximum number of concurrent connections per path
    /// </summary>
    public int MaxConnectionsPerPath { get; set; } = 100;
    
    /// <summary>
    /// Gets or sets the path for WebSocket connections (e.g., "/ws")
    /// </summary>
    public string WebSocketPath { get; set; } = "/ws";
}