CancellationTokenSource _cts = new CancellationTokenSource();
var password = "NILWXLbPUHrzO8pm";
var uri = new Uri($"obsws://192.168.1.38:4455/{password}");
ObsWebsocketClient _socket = new ObsWebsocketClient(uri);

_cts = new CancellationTokenSource();

await _socket.ConnectAsync(_cts.Token);

// The connection has been established
Console.WriteLine("Connection to OBS Studio established!");

while (!(Console.ReadLine() == "exit")) { }

_socket.Dispose();
