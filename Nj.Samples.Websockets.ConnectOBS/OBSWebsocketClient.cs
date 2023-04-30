using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ObsWebsocketClient : IDisposable
{
    private readonly ClientWebSocket _client;
    private readonly Uri _uri;
    private bool _authenticated = false;
    private readonly string _password;

    public ObsWebsocketClient(Uri uri)
    {
        if (!uri.Scheme.Equals("obsws", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The provided uri is not valid for OBS websocket connection.", nameof(uri));
        }

        _password = uri.Segments[1];
        var wsUri = new Uri($"ws://{uri.Host}:{uri.Port}/");
        var queryString = $"password={_password}";

        if (!string.IsNullOrEmpty(wsUri.Query))
        {
            queryString = $"{wsUri.Query}&{queryString}";
        }

        _uri = new Uri($"{wsUri.AbsoluteUri}{uri.AbsolutePath}?{queryString}");

        _client = new ClientWebSocket();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _client.ConnectAsync(_uri, cancellationToken);
        await AuthenticateAsync(cancellationToken);
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(message);
        await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task<string> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1024];
        var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

        if (result.MessageType != WebSocketMessageType.Text)
        {
            throw new InvalidOperationException("Unexpected websocket message type.");
        }

        var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);

        return message;
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        // Send a request to get the authentication requirements
        var authMessage = new
        {
            requestType = "GetAuthRequired",
        };
        var authMessageJson = JsonConvert.SerializeObject(authMessage);
        await SendAsync(authMessageJson, cancellationToken);

        // Receive the authentication requirements response
        var authResponseJson = await ReceiveAsync(cancellationToken);
        var authResponse = JObject.Parse(authResponseJson);
        // Get the authentication challenge and salt
        var requiresAuth = authResponse["d"]["authentication"] != null;
        if (requiresAuth)
        {
            var challenge = authResponse["d"]["authentication"]["challenge"].ToString();
            var salt = authResponse["d"]["authentication"]["salt"].ToString();
            var rpcVersion = int.Parse(authResponse["d"]["rpcVersion"].ToString());

            // Compute the authentication signature
            var signature = ComputeAuthenticationSignature(challenge, _password, salt);
            // Send the authentication response
            var data = JObject.FromObject(new
            {
                rpcVersion = rpcVersion,
                authentication = signature,
                eventSubscriptions = 33
            });

            var authDetails = BuildMessage(MessageTypes.Identify, string.Empty, data, out var msgId);

            var authDetailsJson = JsonConvert.SerializeObject(authDetails);
            await SendAsync(authDetailsJson, cancellationToken);

            // Receive the authentication response
            authResponseJson = await ReceiveAsync(cancellationToken);
            authResponse = JObject.Parse(authResponseJson);

        }

        _authenticated = true;
    }

    private string ComputeAuthenticationSignature(string challenge, string password, string salt)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(password)))
        {
            var challengeBytes = Convert.FromBase64String(challenge);
            var saltBytes = Convert.FromBase64String(salt);
            var hmacInput = new byte[challengeBytes.Length + saltBytes.Length];
            Buffer.BlockCopy(challengeBytes, 0, hmacInput, 0, challengeBytes.Length);
            Buffer.BlockCopy(saltBytes, 0, hmacInput, challengeBytes.Length, saltBytes.Length);
            var signatureBytes = hmac.ComputeHash(hmacInput);
            var signature = Convert.ToBase64String(signatureBytes);
            return signature;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    internal static JObject BuildMessage(MessageTypes opCode, string messageType, JObject additionalFields, out string messageId)
    {
        messageId = Guid.NewGuid().ToString();
        JObject payload = new JObject()
        {
            { "op", (int)opCode }
        };

        JObject data = new JObject();

        switch (opCode)
        {
            case MessageTypes.Request:
                data.Add("requestType", messageType);
                data.Add("requestId", messageId);
                data.Add("requestData", additionalFields);
                additionalFields = null;
                break;
            case MessageTypes.RequestBatch:
                data.Add("requestId", messageId);
                break;

        }

        if (additionalFields != null)
        {
            data.Merge(additionalFields);
        }
        payload.Add("d", data);
        return payload;
    }

    internal enum MessageTypes
    {
        Hello = 0,
        Identify = 1,
        Identified = 2,
        ReIdentify = 3,
        Event = 5,
        Request = 6,
        RequestResponse = 7,
        RequestBatch = 8,
        RequestBatchResponse = 9
    }
}