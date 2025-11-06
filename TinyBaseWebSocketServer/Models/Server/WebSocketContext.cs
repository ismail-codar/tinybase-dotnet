using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Security.Principal;

namespace TinyBaseWebSocketServer.Models.Server
{
    /// <summary>
    /// Represents the context of a WebSocket connection
    /// </summary>
    public abstract class WebSocketContext
    {
        /// <summary>
        /// Gets the underlying WebSocket instance
        /// </summary>
        public abstract WebSocket WebSocket { get; }

        /// <summary>
        /// Gets the HTTP context
        /// </summary>
        public abstract HttpContext HttpContext { get; }

        /// <summary>
        /// Gets whether the connection is authenticated
        /// </summary>
        public abstract bool IsAuthenticated { get; }

        /// <summary>
        /// Gets whether the connection is local
        /// </summary>
        public abstract bool IsLocal { get; }

        /// <summary>
        /// Gets the request URI
        /// </summary>
        public abstract Uri RequestUri { get; }

        /// <summary>
        /// Gets the SecWebSocketVersion
        /// </summary>
        public abstract string SecWebSocketVersion { get; }

        /// <summary>
        /// Gets the Origin
        /// </summary>
        public abstract string? Origin { get; }

        /// <summary>
        /// Gets the SecWebSocketKey
        /// </summary>
        public abstract string? SecWebSocketKey { get; }

        /// <summary>
        /// Gets the headers
        /// </summary>
        public abstract IHeaderDictionary Headers { get; }

        /// <summary>
        /// Gets whether the connection is secure
        /// </summary>
        public abstract bool IsSecureConnection { get; }

        /// <summary>
        /// Gets the supported WebSocket protocols
        /// </summary>
        public abstract IList<string> SecWebSocketProtocols { get; }

        /// <summary>
        /// Gets the user
        /// </summary>
        public abstract IPrincipal? User { get; }

        /// <summary>
        /// Gets the cookie collection
        /// </summary>
        public abstract IRequestCookieCollection? CookieCollection { get; }

        /// <summary>
        /// Gets custom items
        /// </summary>
        public abstract IDictionary<string, object> Items { get; }
    }
}