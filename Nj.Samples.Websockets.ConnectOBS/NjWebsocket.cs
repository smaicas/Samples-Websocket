using System.Net.WebSockets;

namespace Nj.Core.Websocket.Extensions;
public class NjWebsocket : IDisposable
{

    private System.Net.WebSockets.WebSocket? _webSocket;
    private readonly Func<Uri, CancellationToken, Task<WebSocket>> _connectionFactory;

    public NjWebsocket() =>
        _connectionFactory = (Func<Uri, CancellationToken, Task<WebSocket>>)(async (uri, token) =>
        {
            ClientWebSocket client = new ClientWebSocket();
            await client.ConnectAsync(uri, token).ConfigureAwait(false);
            WebSocket webSocket = (WebSocket)client;
            return webSocket;
        });

    public async Task ConnectAsync(Uri uri, CancellationToken ct = default)
    {
        _webSocket = await _connectionFactory(uri, ct);
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(message);
        if (_webSocket != null)
            await (_webSocket.SendAsync(new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1024];
        if (_webSocket != null)
        {
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidOperationException("Unexpected websocket message type.");
            }

            var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);

            return message;
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _webSocket?.Dispose();
    }
}
