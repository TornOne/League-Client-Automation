using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class LeagueClientWebSocket {
	readonly ClientWebSocket socket;
	readonly byte[] socketBuffer = new byte[4096];

	public LeagueClientWebSocket(string[] credentials) {
		while (true) {
			try {
				socket = new ClientWebSocket();
				socket.Options.Credentials = new NetworkCredential("riot", credentials[3]);
				socket.ConnectAsync(new Uri($"wss://127.0.0.1:{credentials[2]}"), CancellationToken.None).Wait();
				break;
			} catch (Exception e) {
				Console.WriteLine($"WebSocket connection failed - {e.Message}");
				Thread.Sleep(3000);
			}
		}
	}

	public void SendMessage(string message) => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

	public async Task<string> ReceiveMessageAsync() {
		StringBuilder message = new StringBuilder(socketBuffer.Length);
		WebSocketReceiveResult result;
		do {
			result = await socket.ReceiveAsync(new ArraySegment<byte>(socketBuffer), CancellationToken.None);
			message.Append(Encoding.UTF8.GetString(socketBuffer, 0, result.Count));
		} while (!result.EndOfMessage);
		return message.ToString();
	}

	public void Subscribe(string eventName) => SendMessage($"[5, \"{eventName}\"]");
}
