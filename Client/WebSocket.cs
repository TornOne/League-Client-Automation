using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Torn.Json;

namespace LCA.Client;
//https://www.mingweisamuel.com/lcu-schema/tool/
static class WebSocket {
	static readonly ClientWebSocket socket = new();
	static byte[] receiveBuffer = new byte[1024];
	static byte[] sendBuffer = new byte[1024];
	static readonly Dictionary<string, Func<string, string, JsonElement, Task>> eventActions = new() {
		//{ "OnJsonApiEvent", AllEvents },
		{ "OnJsonApiEvent_lol-perks_v1_pages", RunePageEvent },
		//{ "OnJsonApiEvent_lol-champ-select_v1_current-champion", CurrentChampionEvent },
		{ "OnJsonApiEvent_lol-champ-select_v1_session", LobbySessionEvent }
	};

	public static async Task Initialize(string[] credentials) {
		//Connect to the client
		while (true) {
			try {
				socket.Options.RemoteCertificateValidationCallback = Http.ValidateCert;
				socket.Options.Credentials = new NetworkCredential("riot", credentials[3]);
				await socket.ConnectAsync(new Uri($"wss://127.0.0.1:{credentials[2]}"), CancellationToken.None);
				break;
			} catch (Exception e) {
				Console.WriteLine($"WebSocket connection failed, retrying - {e.Message}");
				await Task.Delay(3000);
			}
		}

		//Subscribe to the events we care about and start the event listening (and responding) loop
		foreach (string eventName in eventActions.Keys) {
			await Subscribe(eventName);
		}
		Console.WriteLine("Connected to client - Ready to use\n");
		_ = EventLoop().ContinueWith(task => Console.WriteLine($"Event loop terminated:\n{task.Exception?.GetBaseException()}"));
	}

	static Task SendMessage(string message) {
		if (sendBuffer.Length < message.Length * 2) { //Ensure the buffer is large enough
			sendBuffer = new byte[Math.Max(sendBuffer.Length * 2, message.Length * 2)];
		}
		int length = Encoding.UTF8.GetBytes(message, 0, message.Length, sendBuffer, 0);
		return socket.SendAsync(new ArraySegment<byte>(sendBuffer, 0, length), WebSocketMessageType.Text, true, CancellationToken.None);
	}

	static async Task<JsonDocument> ReceiveMessage() {
		int i = 0;
		while (true) {
			WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, i, receiveBuffer.Length - i), CancellationToken.None);
			i += result.Count;
			if (result.EndOfMessage) {
				if (i == 0) { //We get empty messages sometimes. I don't know why. These are not valid json.
					continue;
				}

				try {
					return JsonDocument.Parse(Encoding.UTF8.GetString(receiveBuffer, 0, i));
				} catch {
					Console.WriteLine("Failed to deserialize message:");
					Console.WriteLine(Encoding.UTF8.GetString(receiveBuffer, 0, i));
					i = 0;
				}
			}
			if (i == receiveBuffer.Length) { //Message wasn't read to end because the buffer needs to be enlarged
				byte[] newBuffer = new byte[receiveBuffer.Length * 2];
				Array.Copy(receiveBuffer, newBuffer, receiveBuffer.Length);
				receiveBuffer = newBuffer;
			}
		}
	}

	static Task Subscribe(string eventName) => SendMessage($"[5, \"{eventName}\"]");

	static async Task EventLoop() {
		while (true) {
			using JsonDocument eDoc = await ReceiveMessage();
			JsonElement e = eDoc.RootElement;
			if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 2 &&
				e[1].TryGetValue(out string endpoint) && eventActions.TryGetValue(endpoint, out Func<string, string, JsonElement, Task>? Action) &&
				e[2].ValueKind == JsonValueKind.Object &&
				e[2].TryGetValue("eventType", out JsonElement eventTypeJson) && eventTypeJson.TryGetValue(out string eventType) &&
				e[2].TryGetValue("uri", out JsonElement eventUriJson) && eventUriJson.TryGetValue(out string eventUri) &&
				e[2].TryGetValue("data", out JsonElement data)) {

				await Action(eventType, eventUri, data);
			} else {
				Console.WriteLine("Non-conformant event received:");
				Console.WriteLine(e.ToString(true));
				continue;
			}
		}
	}

	static Task AllEvents(string eventType, string eventUri, JsonElement data) {
		Console.WriteLine(eventType);
		Console.WriteLine(eventUri);
		Console.WriteLine(data.ToString(true));
		return Task.CompletedTask;
	}

	//Fired when a rune page is modified
	//Remember the last edited rune page
	static Task RunePageEvent(string eventType, string eventUri, JsonElement data) {
		if (eventUri.StartsWith("/lol-perks/v1/pages/") && eventType == "Update") {
			State.lastRunes = new RunePage(
				data.GetProperty("primaryStyleId").GetInt32(),
				data.GetProperty("subStyleId").GetInt32(),
				new List<JsonElement>(data.GetProperty("selectedPerkIds").EnumerateArray()).ConvertAll(id => id.GetInt32()));
			Console.WriteLine("Runes updated");
		}
		return Task.CompletedTask;
	}

	//Fired when your currently locked-in champion changes
	/*Broken for a while, switched to LobbySessionEvent
	static async Task CurrentChampionEvent(string eventType, string _, JsonElement data) {
		if ((eventType == "Create" || eventType == "Update") &&
			data.TryGetValue(out int championId) &&
			Champion.idToChampion.TryGetValue(championId, out Champion? champion) &&
			champion != State.currentChampion) {

			State.currentChampion = champion;
			ThirdParty.Data? thirdPartyData = await Actions.LoadChampion(champion, State.currentLane);

			if (!State.modeHasBench && thirdPartyData is not null) {
				Actions.PrintThirdPartyData(thirdPartyData, champion, State.currentLane);
			}
		}
	}*/

	static readonly Dictionary<int, Lane> specialQueues = new() {
		{ 720, Lane.ARAM }
	};

	//Fired for numerous events that occur during champion select
	static async Task LobbySessionEvent(string eventType, string eventUri, JsonElement data) {
		//Get the lane and ban suggestions
		if (eventType == "Create") {
			State.modeHasBench = data.GetProperty("benchEnabled").GetBoolean();
			int queueId = (await Http.Get("/lol-gameflow/v1/session")).AsJson()!.RootElement.GetProperty("gameData").GetProperty("queue").GetProperty("id").GetInt32();
			State.currentLane = specialQueues.TryGetValue(queueId, out Lane currentLane) ? currentLane : Enum.IsDefined(typeof(Lane), queueId) ? (Lane)queueId : Lane.Default;

			if (State.currentLane == Lane.Default) {
				//Only Summoners Rift has actual lanes
				foreach (JsonElement teammate in data.GetProperty("myTeam").EnumerateArray()) {
					if (teammate.GetProperty("summonerId").GetInt64() == State.summonerId) {
						State.currentLane = Champion.LaneFromString(teammate.GetProperty("assignedPosition").GetString()!);
						break;
					}
				}

				if (Config.banSuggestions > 0) {
					await ListBanSuggestions(Lane.Default, "Suggested overall bans:");
					await ListBanSuggestions(State.currentLane, $"Suggested bans for {State.currentLane}:");
				}
			}

			if (Config.eventBanSuggestions > 0 && State.currentLane == Lane.Nexus) {
				await ListBanSuggestions(State.currentLane, $"Suggested bans:");
			}
		}

		if (eventType == "Create" || eventType == "Update") {
			//Find champion ranks for All Random game modes
			if (State.modeHasBench) {
				Dictionary<int, RankInfo> ranks = await ThirdParty.Interface.GetRanks(State.currentLane);
				if (ranks.Count > 0) { //0 means we have failed to fetch ranks in the past, lets not spam their server
					foreach (JsonElement teammate in data.GetProperty("myTeam").EnumerateArray()) {
						int id = teammate.GetProperty("championId").GetInt32();
						if (State.ourChampions.Add(id)) {
							RankInfo rank = ranks[id];
							Console.ForegroundColor = rank.delta > 0.04 ? ConsoleColor.DarkGreen
								: rank.delta > 0.02 ? ConsoleColor.Green
								: rank.delta > -0.02 ? ConsoleColor.White
								: rank.delta > -0.04 ? ConsoleColor.Red
								: ConsoleColor.DarkRed;
							Console.WriteLine(rank.ToString(id));
							Console.ForegroundColor = ConsoleColor.Gray;
						}
					}
				}
			}

			//Detect champion changes
			foreach (JsonElement teammate in data.GetProperty("myTeam").EnumerateArray()) {
				if (teammate.GetProperty("summonerId").GetInt64() == State.summonerId &&
					teammate.GetProperty("championId").TryGetValue(out int championId) &&
					Champion.idToChampion.TryGetValue(championId, out Champion? champion) &&
					champion != State.currentChampion) {

					State.currentChampion = champion;
					ThirdParty.Data? thirdPartyData = await Actions.LoadChampion(champion, State.currentLane);

					if (!State.modeHasBench && thirdPartyData is not null) {
						Actions.PrintThirdPartyData(thirdPartyData, champion, State.currentLane);
					}

					break;
				}
			}
		}
			
		if (eventType == "Delete") {
			if (State.modeHasBench && State.currentChampion is not null && State.currentChampion.TryGetThirdPartyData(State.currentLane, out ThirdParty.Data? thirdPartyData)) {
				Actions.PrintThirdPartyData(thirdPartyData, State.currentChampion, State.currentLane);
			}

			State.Reset();
		}
	}

	static async Task ListBanSuggestions(Lane lane, string title) {
		BanInfo[] bans = await ThirdParty.Interface.GetBanSuggestions(lane);
		if (bans.Length > 0) { //0 means we have failed to fetch ban choices in the past, lets not spam their server
			Console.WriteLine(title);
			foreach (BanInfo ban in bans) {
				Console.WriteLine(ban);
			}
		}
	}
}