using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LCA.Client {
	//TODO: Add champion rank messages for ARAM game modes
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
			//await Subscribe("OnJsonApiEvent"); //All events
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
			HashSet<int> cycledChampions = new HashSet<int>();
			int selectedChampion = 0;

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
					Console.WriteLine("Runes updated");

					//Detect lock-ins
				} else if (endpoint == "OnJsonApiEvent_lol-champ-select_v1_current-champion") {
					if ((eventType == "Create" || eventType == "Update") && contents["data"].TryGet(out int championId)) {
						if (selectedChampion != championId) {
							Champion champion = Champion.idToChampion[championId];
							selectedChampion = champion.id;

							//Find lane (or special game mode) I'm playing
							int queueId = default;
							queueId = (await Http.GetJson("/lol-gameflow/v1/session"))["gameData"]["queue"]["id"].Get<int>();
							Lane lane = LolAlytics.queueToLaneMap.TryGetValue(queueId, out Lane laneName) ? laneName : Lane.Default;
							string query = $"https://lolalytics.com/lol/{champion.name}/{lane.ToString().ToLower()}/build/";

							if (lane == Lane.Default) { //Only need to fetch my lane in the main game mode
								foreach (Json.Object player in (Json.Array)(await Http.GetJson("/lol-champ-select/v1/session"))["myTeam"]) {
									if (player["summonerId"].Get<long>() == State.summonerId) {
										string laneString = player["assignedPosition"].Get<string>();
										if (laneString == "utility") {
											laneString = "support";
										}
										query = $"https://lolalytics.com/lol/{champion.name}/build/?lane={laneString}";
										lane = Champion.LaneFromString(laneString);
										break;
									}
								}
							}
							if (Config.openLolAlytics && !cycledChampions.Contains(selectedChampion)) {
								cycledChampions.Add(selectedChampion);
								System.Diagnostics.Process.Start(query);
							}
							Console.WriteLine($"Selected {champion.fullName} ({lane})");

							//Get data from lolalytics.com
							LolAlytics lolAlyticsData = await champion.GetLolAlytics(lane);
							await Actions.LoadRunePages(champion, lane, lolAlyticsData?.runePage);
							await Actions.PrintLolAlyticsData(lolAlyticsData);
						}
					} else if (eventType == "Delete") {
						selectedChampion = 0;
						cycledChampions.Clear();
					}
				}
			}
		}
	}
}