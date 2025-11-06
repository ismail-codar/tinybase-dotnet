namespace TinyBaseWebSocketServer.Services.Handlers;

/// <summary>
/// Handles message routing and forwarding between WebSocket clients
/// </summary>
public class MessageHandler
{
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly string _serverClientId;
    private readonly Dictionary<string, List<string>> _messageBuffer = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Event raised when a message is received
    /// </summary>
    public event EventHandler<MessageEventArgs>? MessageReceived;
    
    /// <summary>
    /// Event raised when a message is sent
    /// </summary>
    public event EventHandler<MessageEventArgs>? MessageSent;
    
    /// <summary>
    /// Event raised when a message fails to send
    /// </summary>
    public event EventHandler<MessageEventArgs>? MessageSendFailed;

    public MessageHandler(WebSocketConnectionManager connectionManager, string serverClientId = "S")
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _serverClientId = serverClientId ?? throw new ArgumentNullException(nameof(serverClientId));
    }
    
    /// <summary>
    /// Handles a received message from a client
    /// </summary>
    /// <param name="fromClientId">The ID of the client that sent the message</param>
    /// <param name="pathId">The path ID</param>
    /// <param name="payload">The raw message payload</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task HandleMessageAsync(string fromClientId, string pathId, string payload)
    {
        if (string.IsNullOrWhiteSpace(fromClientId))
            throw new ArgumentException("From client ID cannot be null or empty", nameof(fromClientId));
            
        if (string.IsNullOrWhiteSpace(pathId))
            throw new ArgumentException("Path ID cannot be null or empty", nameof(pathId));
            
        if (string.IsNullOrWhiteSpace(payload))
            return; // Ignore empty messages

        // Raise MessageReceived event
        OnMessageReceived(new MessageEventArgs(fromClientId, pathId, payload));
        
        // Create forwarded payload
        var forwardedPayload = MessagePayloadExtensions.CreateRawPayload(fromClientId, payload);
        
        // Parse and route the message
        if (MessagePayloadExtensions.TryParsePayload(payload, out var messagePayload, out var error))
        {
            await RouteMessageAsync(fromClientId, pathId, messagePayload, forwardedPayload);
        }
        else
        {
            // Log error but continue with routing as-is
            await RouteRawMessageAsync(fromClientId, pathId, forwardedPayload);
        }
    }
    
    /// <summary>
    /// Sends a message to a specific client
    /// </summary>
    /// <param name="toClientId">The target client ID</param>
    /// <param name="pathId">The path ID</param>
    /// <param name="payload">The message payload</param>
    /// <returns>True if the message was sent successfully</returns>
    public async Task<bool> SendToClientAsync(string toClientId, string pathId, string payload)
    {
        if (string.IsNullOrWhiteSpace(toClientId))
            return false;
            
        if (string.IsNullOrWhiteSpace(pathId))
            return false;
            
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            var connection = _connectionManager.GetConnection(pathId, toClientId);
            if (connection?.WebSocket?.State == WebSocketState.Open)
            {
                var buffer = System.Text.Encoding.UTF8.GetBytes(payload);
                await connection.WebSocket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
                
                OnMessageSent(new MessageEventArgs(toClientId, pathId, payload));
                return true;
            }
        }
        catch (Exception ex)
        {
            OnMessageSendFailed(new MessageEventArgs(toClientId, pathId, payload));
        }
        
        return false;
    }
    
    /// <summary>
    /// Broadcasts a message to all clients in a path (except the sender)
    /// </summary>
    /// <param name="fromClientId">The ID of the client that sent the message</param>
    /// <param name="pathId">The path ID</param>
    /// <param name="payload">The message payload</param>
    /// <returns>True if at least one message was sent successfully</returns>
    public async Task<bool> BroadcastToPathAsync(string fromClientId, string pathId, string payload)
    {
        if (string.IsNullOrWhiteSpace(fromClientId))
            return false;
            
        if (string.IsNullOrWhiteSpace(pathId))
            return false;
            
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        var connections = _connectionManager.GetConnections(pathId);
        var sent = false;
        
        foreach (var (clientId, connection) in connections)
        {
            if (clientId != fromClientId && connection.WebSocket?.State == WebSocketState.Open)
            {
                try
                {
                    var buffer = System.Text.Encoding.UTF8.GetBytes(payload);
                    await connection.WebSocket.SendAsync(
                        new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                    sent = true;
                }
                catch (Exception ex)
                {
                    // Log error but continue
                }
            }
        }
        
        if (sent)
        {
            OnMessageSent(new MessageEventArgs($"broadcast:{fromClientId}", pathId, payload));
        }
        
        return sent;
    }
    
    /// <summary>
    /// Buffers a message for a client that is not yet ready
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="payload">The message payload</param>
    public void BufferMessage(string pathId, string clientId, string payload)
    {
        if (string.IsNullOrWhiteSpace(pathId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(payload))
            return;

        lock (_lock)
        {
            var key = $"{pathId}:{clientId}";
            if (!_messageBuffer.ContainsKey(key))
            {
                _messageBuffer[key] = new List<string>();
            }
            _messageBuffer[key].Add(payload);
        }
    }
    
    /// <summary>
    /// Gets and clears buffered messages for a client
    /// </summary>
    /// <param name="pathId">The path ID</param>
    /// <param name="clientId">The client ID</param>
    /// <returns>The list of buffered messages</returns>
    public IReadOnlyList<string> GetAndClearBufferedMessages(string pathId, string clientId)
    {
        if (string.IsNullOrWhiteSpace(pathId) || string.IsNullOrWhiteSpace(clientId))
            return new List<string>();

        lock (_lock)
        {
            var key = $"{pathId}:{clientId}";
            if (_messageBuffer.TryGetValue(key, out var messages))
            {
                _messageBuffer.Remove(key);
                return messages.AsReadOnly();
            }
        }
        
        return new List<string>();
    }
    
    /// <summary>
    /// Routes a message based on its payload
    /// </summary>
    private async Task RouteMessageAsync(string fromClientId, string pathId, MessagePayload messagePayload, string forwardedPayload)
    {
        // Handle different routing scenarios
        if (messagePayload.IsBroadcast)
        {
            // Send to all clients except sender
            await BroadcastToPathAsync(fromClientId, pathId, forwardedPayload);
        }
        else if (messagePayload.IsServerMessage)
        {
            // Send to server (buffer for server to consume)
            BufferMessage(pathId, messagePayload.ToClientId, forwardedPayload);
        }
        else
        {
            // Send to specific client
            await SendToClientAsync(messagePayload.ToClientId, pathId, forwardedPayload);
        }
    }
    
    /// <summary>
    /// Routes a raw message when parsing fails
    /// </summary>
    private async Task RouteRawMessageAsync(string fromClientId, string pathId, string forwardedPayload)
    {
        // For unparseable messages, broadcast to all clients
        await BroadcastToPathAsync(fromClientId, pathId, forwardedPayload);
    }
    
    protected virtual void OnMessageReceived(MessageEventArgs e)
    {
        MessageReceived?.Invoke(this, e);
    }
    
    protected virtual void OnMessageSent(MessageEventArgs e)
    {
        MessageSent?.Invoke(this, e);
    }
    
    protected virtual void OnMessageSendFailed(MessageEventArgs e)
    {
        MessageSendFailed?.Invoke(this, e);
    }
}