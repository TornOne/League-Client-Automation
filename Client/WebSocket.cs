using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LCA.Client {
	static class WebSocket {
		static ClientWebSocket socket;
		static byte[] receiveBuffer = new byte[1024];
		static byte[] sendBuffer = new byte[1024];

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
			await Subscribe("OnJsonApiEvent_lol-perks_v1_pages");
			await Subscribe("OnJsonApiEvent_lol-champ-select_v1_current-champion");
			await Subscribe("OnJsonApiEvent_lol-champ-select_v1_session");
			//await Subscribe("OnJsonApiEvent"); //All events

			Console.WriteLine("Connected to client\n");
			_ = EventLoop().ContinueWith(task => Console.WriteLine($"Event loop terminated:\n{task.Exception.GetBaseException()}"));
		}

		static Task SendMessage(string message) {
			if (sendBuffer.Length < message.Length * 2) { //Ensure the buffer is large enough
				sendBuffer = new byte[sendBuffer.Length * 2];
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
			HashSet<int> ourChampions = new HashSet<int>();

			while (true) {
				Json.Node eNode = await ReceiveMessage();
				if (!(eNode is Json.Array e && e.Count > 2 &&
					e[1].TryGet(out string endpoint) &&
					e[2] is Json.Object contents &&
					contents.TryGetValue("eventType", out Json.Node eventTypeJson) && eventTypeJson.TryGet(out string eventType) &&
					contents.TryGetValue("uri", out Json.Node eventUriJson) && eventUriJson.TryGet(out string eventUri))) {

					Console.WriteLine("Non-conformant event received:");
					Console.WriteLine(eNode.ToString(true));
					continue;
				}

				//Remember the last edited rune page
				if (endpoint == "OnJsonApiEvent_lol-perks_v1_pages" &&
					eventUri.StartsWith("/lol-perks/v1/pages/") &&
					eventType == "Update") {

					Json.Node data = contents["data"];
					State.lastRunes = new RunePage(data["primaryStyleId"].Get<int>(), data["subStyleId"].Get<int>(), new List<Json.Node>((Json.Array)data["selectedPerkIds"]).ConvertAll(id => id.Get<int>()));

				//Detect lock-ins
				} else if (endpoint == "OnJsonApiEvent_lol-champ-select_v1_current-champion") {
					if ((eventType == "Create" || eventType == "Update") && contents["data"].TryGet(out int championId) && Champion.idToChampion.TryGetValue(championId, out Champion champion)) {
						if (champion != State.currentChampion) {
							State.currentChampion = champion;
							LolAlytics lolAlytics = await Actions.LoadChampion(champion, State.currentLane);

							if (State.currentLane != Lane.ARAM) {
								Actions.PrintLolAlytics(lolAlytics, champion, State.currentLane);
							}
						}
					}

				//Observe champion select
				} else if (endpoint == "OnJsonApiEvent_lol-champ-select_v1_session") {
					if (eventType == "Create") {
						int queueId = (await Http.GetJson("/lol-gameflow/v1/session"))["gameData"]["queue"]["id"].Get<int>();
						State.currentLane = Enum.IsDefined(typeof(Lane), queueId) ? (Lane)queueId : Lane.Default;

						if (State.currentLane == Lane.Default) {
							//Only Summoners Rift has actual lanes
							foreach (Json.Object teammate in (Json.Array)contents["data"]["myTeam"]) {
								if (teammate["summonerId"].Get<long>() == State.summonerId) {
									State.currentLane = Champion.LaneFromString(teammate["assignedPosition"].Get<string>());
									break;
								}
							}

							//TODO: Add ban suggestions for other game modes
							if (Config.banSuggestions > 0) {
								await ListBanSuggestions(Lane.Default, "Suggested overall bans:");
								await ListBanSuggestions(State.currentLane, $"Suggested bans for {State.currentLane}:");
							}
						}
					}

					if (eventType == "Create" || eventType == "Update") {
						if (State.currentLane == Lane.ARAM) {
							if (LolAlytics.aramRanks is null) {
								await LolAlytics.FetchAramRanks();
							}
							if (LolAlytics.aramRanks.Count > 0) { //0 means we have failed to fetch ARAM ranks in the past, lets not spam their server
								foreach (Json.Object teammate in (Json.Array)contents["data"]["myTeam"]) {
									int id = teammate["championId"].Get<int>();
									if (ourChampions.Add(id)) {
										(int rank, double wr, double delta) = LolAlytics.aramRanks[id];
										Console.WriteLine($"{Champion.idToChampion[id].fullName,-12} - Rank {rank,3}, WR: {wr:0.0%} ({(delta >= 0 ? "+" : "")}{delta:0.0%})");
									}
								}
							}
						}
					} else if (eventType == "Delete") {
						if (State.currentLane == Lane.ARAM && State.currentChampion.lolAlyticsInfo.TryGetValue(State.currentLane, out LolAlytics lolAlytics)) {
							Actions.PrintLolAlytics(lolAlytics, State.currentChampion, State.currentLane);
						}

						State.currentChampion = null;
						ourChampions.Clear();
						State.currentLane = Lane.Default;
					}
				}
			}
		}

		static async Task ListBanSuggestions(Lane lane, string title) {
			if (!LolAlytics.banSuggestions.TryGetValue(lane, out (int id, double pbi)[] topBans)) {
				await LolAlytics.FetchBanChoices(lane);
				topBans = LolAlytics.banSuggestions[lane];
			}
			if (topBans.Length > 0) { //0 means we have failed to fetch ban choices in the past, lets not spam their server
				Console.WriteLine(title);
				for (int i = 0; i < Config.banSuggestions; i++) {
					(int id, double pbi) = LolAlytics.banSuggestions[lane][i];
					Console.WriteLine($"{Champion.idToChampion[id].fullName,-12} - {pbi,2:0}");
				}
			}
		}
	}
}