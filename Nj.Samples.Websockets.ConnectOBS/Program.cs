using Nj.Core.Websocket.Extensions;

CancellationTokenSource _cts = new CancellationTokenSource();
var password = "NILWXLbPUHrzO8pm";
var uri = new Uri($"ws://192.168.1.38:4455");
NjWebsocket socket = new();

_cts = new CancellationTokenSource();

await socket.ConnectAsync(uri);
var res = await socket.ReceiveAsync();

// The connection has been established
Console.WriteLine("Connection to OBS Studio established!");

while (Console.ReadLine() != "exit") { }
socket.Dispose();