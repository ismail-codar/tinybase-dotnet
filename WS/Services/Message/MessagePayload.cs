namespace TinyBaseWebSocketServer.Services.Message;

/// <summary>
/// Represents a parsed message payload
/// </summary>
public class MessagePayload
{
    /// <summary>
    /// Gets the target client ID (empty string for broadcast, "S" for server)
    /// </summary>
    public string ToClientId { get; }
    
    /// <summary>
    /// Gets the request ID for this message
    /// </summary>
    public string? RequestId { get; }
    
    /// <summary>
    /// Gets the message type
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Gets the message body (if any)
    /// </summary>
    public string? Body { get; }

    public MessagePayload(string toClientId, string? requestId, string message, string? body = null)
    {
        ToClientId = toClientId ?? throw new ArgumentNullException(nameof(toClientId));
        RequestId = requestId;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Body = body;
    }

    /// <summary>
    /// Creates a broadcast payload (empty target client ID)
    /// </summary>
    public static MessagePayload Broadcast(string? requestId, string message, string? body = null)
    {
        return new MessagePayload(string.Empty, requestId, message, body);
    }

    /// <summary>
    /// Creates a server payload
    /// </summary>
    public static MessagePayload ToServer(string? requestId, string message, string? body = null)
    {
        return new MessagePayload("S", requestId, message, body);
    }

    /// <summary>
    /// Creates a client-to-client payload
    /// </summary>
    public static MessagePayload ToClient(string clientId, string? requestId, string message, string? body = null)
    {
        return new MessagePayload(clientId, requestId, message, body);
    }

    /// <summary>
    /// Serializes this payload to a string
    /// </summary>
    public string Serialize()
    {
        var parts = new List<string> { ToClientId };
        
        if (RequestId != null)
        {
            parts.Add(RequestId);
        }
        
        parts.Add(Message);
        
        if (Body != null)
        {
            parts.Add(Body);
        }
        
        return string.Join("|", parts);
    }

    /// <summary>
    /// Deserializes a payload string to a MessagePayload
    /// </summary>
    /// <param name="payload">The payload string to deserialize</param>
    /// <returns>The deserialized MessagePayload</returns>
    /// <exception cref="ArgumentException">If the payload format is invalid</exception>
    public static MessagePayload Deserialize(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or empty", nameof(payload));
            
        var parts = payload.Split('|');
        if (parts.Length < 2)
            throw new ArgumentException("Invalid payload format", nameof(payload));
            
        var toClientId = parts[0];
        var requestId = parts.Length > 2 ? parts[1] : null;
        var message = parts.Length > 2 ? parts[2] : parts[1];
        var body = parts.Length > 3 ? parts[3] : null;
        
        return new MessagePayload(toClientId, requestId, message, body);
    }
}

/// <summary>
/// Extensions for MessagePayload
/// </summary>
public static class MessagePayloadExtensions
{
    private const string ServerClientId = "S";
    private const char PayloadSeparator = '|';
    
    /// <summary>
    /// Creates a raw payload with client ID and payload data
    /// </summary>
    public static string CreateRawPayload(string fromClientId, string payload)
    {
        if (string.IsNullOrWhiteSpace(fromClientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(fromClientId));
            
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or empty", nameof(payload));
            
        return $"{fromClientId}{PayloadSeparator}{payload}";
    }
    
    /// <summary>
    /// Creates a complete payload with client routing
    /// </summary>
    public static string CreateRoutingPayload(string toClientId, string? requestId, string message, string? body = null)
    {
        if (string.IsNullOrWhiteSpace(toClientId))
            throw new ArgumentException("Target client ID cannot be null or empty", nameof(toClientId));
            
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));
            
        var parts = new List<string> { toClientId };
        
        if (requestId != null)
        {
            parts.Add(requestId);
        }
        
        parts.Add(message);
        
        if (body != null)
        {
            parts.Add(body);
        }
        
        return string.Join(PayloadSeparator.ToString(), parts);
    }
    
    /// <summary>
    /// Validates and parses a payload string
    /// </summary>
    public static bool TryParsePayload(string payload, out MessagePayload? messagePayload, out string? error)
    {
        error = null;
        messagePayload = null;
        
        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "Payload is null or empty";
            return false;
        }
        
        try
        {
            var parts = payload.Split(PayloadSeparator);
            if (parts.Length < 2)
            {
                error = "Invalid payload format - insufficient parts";
                return false;
            }
            
            var toClientId = parts[0];
            var requestId = parts.Length > 2 ? parts[1] : null;
            var message = parts.Length > 2 ? parts[2] : parts[1];
            var body = parts.Length > 3 ? parts[3] : null;
            
            messagePayload = new MessagePayload(toClientId, requestId, message, body);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to parse payload: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// Checks if this is a server message (targeting the server client)
    /// </summary>
    public static bool IsServerMessage(this MessagePayload payload)
    {
        return payload?.ToClientId == ServerClientId;
    }
    
    /// <summary>
    /// Checks if this is a broadcast message (empty target)
    /// </summary>
    public static bool IsBroadcast(this MessagePayload payload)
    {
        return payload != null && string.IsNullOrEmpty(payload.ToClientId);
    }
}