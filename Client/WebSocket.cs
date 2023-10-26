using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LCA.Client {
	//https://www.mingweisamuel.com/lcu-schema/tool/
	static class WebSocket {
		static ClientWebSocket socket;
		static byte[] receiveBuffer = new byte[1024];
		static byte[] sendBuffer = new byte[1024];
		static readonly Dictionary<string, Func<string, string, Json.Node, Task>> eventActions = new Dictionary<string, Func<string, string, Json.Node, Task>> {
			//{ "OnJsonApiEvent", AllEvents },
			{ "OnJsonApiEvent_lol-perks_v1_pages", RunePageEvent },
			{ "OnJsonApiEvent_lol-champ-select_v1_current-champion", CurrentChampionEvent },
			{ "OnJsonApiEvent_lol-champ-select_v1_session", LobbySessionEvent }
		};

		public static async Task Initialize(string[] credentials) {
			//Connect to the client
			while (true) {
				try {
					socket = new ClientWebSocket();
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
			_ = EventLoop().ContinueWith(task => Console.WriteLine($"Event loop terminated:\n{task.Exception.GetBaseException()}"));
		}

		static Task SendMessage(string message) {
			if (sendBuffer.Length < message.Length * 2) { //Ensure the buffer is large enough
				sendBuffer = new byte[Math.Max(sendBuffer.Length * 2, message.Length * 2)];
			}
			int length = Encoding.UTF8.GetBytes(message, 0, message.Length, sendBuffer, 0);
			return socket.SendAsync(new ArraySegment<byte>(sendBuffer, 0, length), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		static async Task<Json.Node> ReceiveMessage() {
			int i = 0;
			while (true) {
				WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, i, receiveBuffer.Length - i), CancellationToken.None);
				i += result.Count;
				if (result.EndOfMessage) {
					if (i == 0) { //We get empty messages sometimes. I don't know why. These are not valid json.
						continue;
					}

					try {
						return Json.Node.Parse(Encoding.UTF8.GetString(receiveBuffer, 0, i));
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
				Json.Node eNode = await ReceiveMessage();
				if (eNode is Json.Array e && e.Count > 2 &&
					e[1].TryGet(out string endpoint) && eventActions.TryGetValue(endpoint, out var Action) &&
					e[2] is Json.Object contents &&
					contents.TryGetValue("eventType", out Json.Node eventTypeJson) && eventTypeJson.TryGet(out string eventType) &&
					contents.TryGetValue("uri", out Json.Node eventUriJson) && eventUriJson.TryGet(out string eventUri) &&
					contents.TryGetValue("data", out Json.Node data)) {

					await Action(eventType, eventUri, data);
				} else {
					Console.WriteLine("Non-conformant event received:");
					Console.WriteLine(eNode.ToString(true));
					continue;
				}
			}
		}

		static Task AllEvents(string eventType, string eventUri, Json.Node data) {
			Console.WriteLine(eventType);
			Console.WriteLine(eventUri);
			Console.WriteLine(data.ToString(true));
			return Task.CompletedTask;
		}

		//Fired when a rune page is modified
		//Remember the last edited rune page
		static Task RunePageEvent(string eventType, string eventUri, Json.Node data) {
			if (eventUri.StartsWith("/lol-perks/v1/pages/") && eventType == "Update") {
				State.lastRunes = new RunePage(
					data["primaryStyleId"].Get<int>(),
					data["subStyleId"].Get<int>(),
					new List<Json.Node>((Json.Array)data["selectedPerkIds"]).ConvertAll(id => id.Get<int>()));
				Console.WriteLine("Runes updated");
			}
			return Task.CompletedTask;
		}

		//Fired when your currently locked-in champion changes
		//TODO: This might be broken, so you might need to parse this out of the session event below
		static async Task CurrentChampionEvent(string eventType, string eventUri, Json.Node data) {
			if ((eventType == "Create" || eventType == "Update") &&
				data.TryGet(out int championId) &&
				Champion.idToChampion.TryGetValue(championId, out Champion champion) &&
				champion != State.currentChampion) {

				State.currentChampion = champion;
				LolAlytics lolAlytics = await Actions.LoadChampion(champion, State.currentLane);

				if (!State.modeHasBench && lolAlytics != null) {
					Actions.PrintLolAlytics(lolAlytics, champion, State.currentLane);
				}
			}
		}

		//Fired for numerous events that occur during champion select
		static async Task LobbySessionEvent(string eventType, string eventUri, Json.Node data) {
			if (eventType == "Create") {
				State.modeHasBench = data["benchEnabled"].Get<bool>();
				int queueId = (await Http.GetJson("/lol-gameflow/v1/session"))["gameData"]["queue"]["id"].Get<int>();
				State.currentLane = Enum.IsDefined(typeof(Lane), queueId) ? (Lane)queueId : Lane.Default;

				if (State.currentLane == Lane.Default) {
					//Only Summoners Rift has actual lanes
					foreach (Json.Object teammate in (Json.Array)data["myTeam"]) {
						if (teammate["summonerId"].Get<long>() == State.summonerId) {
							State.currentLane = Champion.LaneFromString(teammate["assignedPosition"].Get<string>());
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
				if (State.modeHasBench) {
					Dictionary<int, RankInfo> ranks = await LolAlytics.GetRanks(State.currentLane);
					if (ranks.Count > 0) { //0 means we have failed to fetch ranks in the past, lets not spam their server
						foreach (Json.Object teammate in (Json.Array)data["myTeam"]) {
							int id = teammate["championId"].Get<int>();
							if (State.ourChampions.Add(id)) {
								RankInfo rank = ranks[id];
								Console.ForegroundColor = rank.delta > 0.035 ? ConsoleColor.DarkGreen
									: rank.delta > 0.015 ? ConsoleColor.Green
									: rank.delta > -0.015 ? ConsoleColor.White
									: rank.delta > -0.035 ? ConsoleColor.Red
									: ConsoleColor.DarkRed;
								Console.WriteLine(rank.ToString(id));
								Console.ForegroundColor = ConsoleColor.Gray;
							}
						}
					}
				}
			}
			
			if (eventType == "Delete") {
				if (State.modeHasBench && State.currentChampion.TryGetLolAlytics(State.currentLane, out LolAlytics lolAlytics)) {
					Actions.PrintLolAlytics(lolAlytics, State.currentChampion, State.currentLane);
				}

				State.Reset();
			}
		}

		static async Task ListBanSuggestions(Lane lane, string title) {
			BanInfo[] bans = await LolAlytics.GetBanSuggestions(lane);
			if (bans.Length > 0) { //0 means we have failed to fetch ban choices in the past, lets not spam their server
				Console.WriteLine(title);
				foreach (BanInfo ban in bans) {
					Console.WriteLine(ban);
				}
			}
		}
	}
}