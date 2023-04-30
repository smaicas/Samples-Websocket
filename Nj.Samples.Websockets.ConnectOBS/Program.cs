using Nj.Core.Websocket.Extensions;

CancellationTokenSource _cts = new CancellationTokenSource();
var password = "";
var uri = new Uri($"ws://127.0.0.1:4455");
NjWebsocket socket = new();

_cts = new CancellationTokenSource();

await socket.ConnectAsync(uri);
var res = await socket.ReceiveAsync();

// The connection has been established
Console.WriteLine("Connection to OBS Studio established!");

while (Console.ReadLine() != "exit") { }
socket.Dispose();