using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Torn.Json;

// TODO:
// More LolAlytics integration
// - Fetch item sets
// - - Compare item sets

//http://www.mingweisamuel.com/lcu-schema/tool/
class Program {
	const string riotCert = "MIIEIDCCAwgCCQDJC+QAdVx4UDANBgkqhkiG9w0BAQUFADCB0TELMAkGA1UEBhMCVVMxEzARBgNVBAgTCkNhbGlmb3JuaWExFTATBgNVBAcTDFNhbnRhIE1vbmljYTETMBEGA1UEChMKUmlvdCBHYW1lczEdMBsGA1UECxMUTG9MIEdhbWUgRW5naW5lZXJpbmcxMzAxBgNVBAMTKkxvTCBHYW1lIEVuZ2luZWVyaW5nIENlcnRpZmljYXRlIEF1dGhvcml0eTEtMCsGCSqGSIb3DQEJARYeZ2FtZXRlY2hub2xvZ2llc0ByaW90Z2FtZXMuY29tMB4XDTEzMTIwNDAwNDgzOVoXDTQzMTEyNzAwNDgzOVowgdExCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpDYWxpZm9ybmlhMRUwEwYDVQQHEwxTYW50YSBNb25pY2ExEzARBgNVBAoTClJpb3QgR2FtZXMxHTAbBgNVBAsTFExvTCBHYW1lIEVuZ2luZWVyaW5nMTMwMQYDVQQDEypMb0wgR2FtZSBFbmdpbmVlcmluZyBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkxLTArBgkqhkiG9w0BCQEWHmdhbWV0ZWNobm9sb2dpZXNAcmlvdGdhbWVzLmNvbTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKoJemF/6PNG3GRJGbjzImTdOo1OJRDI7noRwJgDqkaJFkwv0X8aPUGbZSUzUO23cQcCgpYj21ygzKu5dtCN2EcQVVpNtyPuM2V4eEGr1woodzALtufL3Nlyh6g5jKKuDIfeUBHvJNyQf2h3Uha16lnrXmz9o9wsX/jf+jUAljBJqsMeACOpXfuZy+YKUCxSPOZaYTLCy+0GQfiT431pJHBQlrXAUwzOmaJPQ7M6mLfsnpHibSkxUfMfHROaYCZ/sbWKl3lrZA9DbwaKKfS1Iw0ucAeDudyuqb4JntGU/W0aboKA0c3YB02mxAM4oDnqseuKV/CX8SQAiaXnYotuNXMCAwEAATANBgkqhkiG9w0BAQUFAAOCAQEAf3KPmddqEqqC8iLslcd0euC4F5+USp9YsrZ3WuOzHqVxTtX3hR1scdlDXNvrsebQZUqwGdZGMS16ln3kWObw7BbhU89tDNCN7Lt/IjT4MGRYRE+TmRc5EeIXxHkQ78bQqbmAI3GsW+7kJsoOq3DdeE+M+BUJrhWorsAQCgUyZO166SAtKXKLIcxa+ddC49NvMQPJyzm3V+2b1roPSvD2WV8gRYUnGmy/N0+u6ANq5EsbhZ548zZc+BI4upsWChTLyxt2RxR7+uGlS1+5EcGfKZ+g024k/J32XP4hdho7WYAS2xMiV83CfLR/MNi8oSMaVQTdKD8cpgiWJk3LXWehWA==";
	static long summonerId;
	static HttpClient http;
	static LeagueClientWebSocket socket;

	static RunePage lastRunes;
	static bool openLolAlytics, setSummonerSpells;

	static async Task Main() {
		System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

		//Load in all the configuration settings
		string allText = File.ReadAllText("config.json");
		Dictionary<string, object> settings = Json.Deserialize(allText) as Dictionary<string, object>;
		string lockfilePath = settings["installPath"] + "lockfile";
		openLolAlytics = (bool)settings["openLolAlytics"];
		setSummonerSpells = (bool)settings["setSummonerSpells"];
		List<object> spellOrder = settings["spellOrder"] as List<object>;
		for (int i = 0; i < spellOrder.Count; i++) {
			if (Enum.TryParse(spellOrder[i] as string, out Spell spell)) {
				LolAlytics.spellToOrderMap[spell] = i;
			}
		}
		Console.WriteLine("Settings loaded");

		//Load in all the rune pages
		Champion.Load();
		Console.WriteLine("Runes loaded");

		//Make sure the game is running and find the credentials
		while (!File.Exists(lockfilePath)) {
			await Task.Delay(1000);
		}
		string[] credentials;
		using (StreamReader lockfileStream = new StreamReader(new FileStream(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128))) {
			credentials = lockfileStream.ReadToEnd().Split(':'); //[0]processname:[1]processid:[2]port:[3]password:[4]protocol
		}
		Console.WriteLine($"Found credentials - {string.Join(":", credentials)}");

		//(Hack way to) trust Riot's self-signed certificate
		ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => errors == SslPolicyErrors.None || //Either it's error-free
			errors == SslPolicyErrors.RemoteCertificateChainErrors && chain.ChainStatus.Length == 1 && chain.ChainStatus[0].Status == X509ChainStatusFlags.UntrustedRoot && //Or the problem is (only) in the chain having an untrusted root
			Convert.ToBase64String(chain.ChainElements[chain.ChainElements.Count - 1].Certificate.RawData) == riotCert; //which is the listed Riot certificate
		//Alternatively trust all certificates
		//ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;

		//Connect to the client via both HTTP and WebSocket
		http = new HttpClient {
			BaseAddress = new Uri($"https://127.0.0.1:{credentials[2]}"),
		};
		http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Array.ConvertAll($"riot:{credentials[3]}".ToCharArray(), c => (byte)c)));
		socket = new LeagueClientWebSocket(credentials);
		Console.WriteLine("Connected to client");

		//Fetch summoner ID
		await Task.Delay(6000); //TODO: Hack delay to wait until the client is actually finished booting up
		summonerId = long.Parse(Json.LazyParseObject(http.GetStringAsync("/lol-summoner/v1/current-summoner").Result)["summonerId"]);
		Console.WriteLine($"Logged in as summoner {summonerId}\n");

		//Subscribe to the events we care about and start the event listening (and responding) loop
		socket.Subscribe("OnJsonApiEvent_lol-perks_v1_pages");
		socket.Subscribe("OnJsonApiEvent_lol-champ-select_v1_current-champion");
		//socket.Subscribe("OnJsonApiEvent"); //All events
		_ = EventLoop().ContinueWith(task => Console.WriteLine($"Event loop terminated:\n{task.Exception.GetBaseException()}"));

		while (true) {
			string[] command = Console.ReadLine().Split(new[] { ' ' }, 3);
			if (command.Length < 2) {
				Console.WriteLine($"Too few arguments: \"{command[0]}\"");
			} else if (command[0] == "save") {
				Console.WriteLine(Champion.SaveRunePage(command[1], lastRunes, command.Length > 2 ? Champion.LaneFromString(command[2]) : Lane.Default) ? "Rune page successfully saved" : "No such champion found");
			} else if (command[0] == "load") {
				Champion champion = Champion.FindByPartialName(command[1]);
				if (champion is null) {
					Console.WriteLine("No such champion found");
				} else {
					Lane lane = command.Length > 2 ? Champion.LaneFromString(command[2]) : Lane.Default;
					LolAlytics lolAlyticsData = await champion.GetLolAlytics(lane);
					await LoadRunePages(champion, lane, lolAlyticsData?.runePage);
					await PrintLolAlyticsData(lolAlyticsData);
				}
			} else if (command[0] == "get") {
				string result = http.GetStringAsync(command[1]).Result;
				if (result.Length > 0) {
					Console.WriteLine(Json.PrettyPrint(result));
				}
			} else if (command[0] == "delete") {
				string result = await http.DeleteAsync(command[1]).Result.Content.ReadAsStringAsync();
				if (result.Length > 0) {
					Console.WriteLine(Json.PrettyPrint(result));
				}
			} else if (command[0] == "put") {
				StringContent request = new StringContent(command[2]);
				request.Headers.ContentType.MediaType = "application/json";
				string result = await http.PutAsync(command[1], request).Result.Content.ReadAsStringAsync();
				if (result.Length > 0) {
					Console.WriteLine(Json.PrettyPrint(result));
				}
			} else if (command[0] == "post") {
				StringContent request = new StringContent(command[2]);
				request.Headers.ContentType.MediaType = "application/json";
				string result = await http.PostAsync(command[1], request).Result.Content.ReadAsStringAsync();
				if (result.Length > 0) {
					Console.WriteLine(Json.PrettyPrint(result));
				}
			} else {
				Console.WriteLine($"Unknown command: \"{string.Join(" ", command)}\"");
			}
			Console.WriteLine();
		}
	}

	static async Task EventLoop() {
		int selectedChampion = 0;

		while (true) {
			string eventJson = await socket.ReceiveMessageAsync();
			if (eventJson.Length == 0) {
				continue;
			}
			if (!(Json.Deserialize(eventJson) is List<object> e)) {
				Console.WriteLine("Failed to deserialize message:");
				Console.WriteLine(eventJson);
				continue;
			}
			if (!(e[1] is string endpoint &&
				e[2] is Dictionary<string, object> contents &&
				contents["eventType"] is string eventType &&
				contents["uri"] is string eventURI)) {
				Console.WriteLine("Non-conformant event received:");
				Console.WriteLine(Json.PrettyPrint(e));
				continue;
			}

			if (endpoint == "OnJsonApiEvent_lol-perks_v1_pages" && //Remember the last edited rune page
				eventURI.StartsWith("/lol-perks/v1/pages/") &&
				eventType == "Update") {

				Dictionary<string, object> data = contents["data"] as Dictionary<string, object>;
				lastRunes = new RunePage((int)data["primaryStyleId"], (int)data["subStyleId"], (data["selectedPerkIds"] as List<object>).ConvertAll(x => (int)x));
				Console.WriteLine("Runes updated");

			} else if (endpoint == "OnJsonApiEvent_lol-champ-select_v1_current-champion") { //Detect lock-ins
				if ((eventType == "Create" || eventType == "Update") && contents["data"] is int championId) {
					if (selectedChampion != championId) {
						Champion champion = Champion.idToChampion[championId];
						selectedChampion = champion.id;

						//Find lane (or special game mode) I'm playing
						int queueId = int.Parse(Json.LazyParseObject(Json.LazyParseObject(Json.LazyParseObject(await http.GetStringAsync("/lol-gameflow/v1/session"))["gameData"])["queue"])["id"]);
						Lane lane = LolAlytics.queueToLaneMap.TryGetValue(queueId, out Lane laneName) ? laneName : Lane.Default;
						string query = $"https://lolalytics.com/lol/{champion.name}/{lane.ToString().ToLower()}/build/";

						if (lane == Lane.Default) { //Only need to fetch my lane in a non-special game mode
							foreach (Dictionary<string, object> player in Json.Deserialize(Json.LazyParseObject(await http.GetStringAsync("/lol-champ-select/v1/session"))["myTeam"]) as List<object>) {
								if ((player["summonerId"] is int id ? id : (long)player["summonerId"]) == summonerId) {
									query = $"https://lolalytics.com/lol/{champion.name}/build/?lane={player["assignedPosition"]}";
									lane = Champion.LaneFromString(player["assignedPosition"] as string);
									break;
								}
							}
						}
						if (openLolAlytics) {
							Process.Start(query);
						}
						Console.WriteLine($"Selected {champion.fullName} ({lane})");

						//Get data from lolalytics.com
						LolAlytics lolAlyticsData = await champion.GetLolAlytics(lane);
						await LoadRunePages(champion, lane, lolAlyticsData?.runePage);
						await PrintLolAlyticsData(lolAlyticsData);
					}
				} else if (eventType == "Delete") {
					selectedChampion = 0;
				}
			}
		}
	}

	static async Task LoadRunePages(Champion champion, Lane lane, RunePage lolAlyticsRunePage) {
		await FreePages(2);

		if (lolAlyticsRunePage is null) {
			Console.WriteLine("LolAlytics rune page not found");
		} else if (await CreateRunePage(lolAlyticsRunePage, $"{champion.fullName} {lane}")) {
			Console.WriteLine("LolAlytics rune page loaded");
		} else {
			Console.WriteLine("LolAlytics rune page loading failed");
		}

		if (!champion.TryGetRunePage(out RunePage runePage, lane)) {
			Console.WriteLine("Preset rune page not found");
		} else if (await CreateRunePage(runePage, champion.fullName)) {
			Console.WriteLine("Preset rune page loaded");
		} else {
			Console.WriteLine("Preset rune page loading failed");
		}

		if (runePage != null && lolAlyticsRunePage != null) {
			int differingRuneCount = 0;
			for (int i = 0; i < 6; i++) {
				if (!Array.Exists(lolAlyticsRunePage.runes, rune => rune == runePage.runes[i])) {
					differingRuneCount++;
				}
			}
			for (int i = 6; i < 9; i++) {
				if (runePage.runes[i] != lolAlyticsRunePage.runes[i]) {
					differingRuneCount++;
				}
			}
			
			if (differingRuneCount > 1) {
				Console.WriteLine($"{differingRuneCount} runes differ between preset and LolAlytics");
			}
		}
	}

	static async Task FreePages(int amount) {
		int maxPages = int.Parse(Json.LazyParseObject(await http.GetStringAsync("/lol-perks/v1/inventory"))["ownedPageCount"]);
		List<string> pages = Json.LazyParseArray(await http.GetStringAsync("/lol-perks/v1/pages"));

		for (int i = maxPages - amount; i < pages.Count - 5; i++) {
			await http.DeleteAsync("/lol-perks/v1/pages/" + Json.LazyParseObject(pages[i])["id"]);
		}
	}

	static async Task<bool> CreateRunePage(RunePage runePage, string pageName) {
		StringContent request = new StringContent(Json.Serialize(new Dictionary<string, object> {
			{ "autoModifiedSelections", Array.Empty<int>() },
			{ "current", true },
			{ "id", 0 },
			{ "isActive", true },
			{ "isDeletable", true },
			{ "isEditable", true },
			{ "isValid", true },
			{ "lastModified", (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds },
			{ "name", pageName },
			{ "order", 0 },
			{ "primaryStyleId", runePage.PrimaryStyle },
			{ "subStyleId", runePage.SubStyle },
			{ "selectedPerkIds", runePage.runes }
		}));
		request.Headers.ContentType.MediaType = "application/json";

		return (await http.PostAsync($"/lol-perks/v1/pages", request)).IsSuccessStatusCode;
	}

	static async Task UpdateSummonerSpells(int spell1Id, int spell2Id) {
		StringContent request = new StringContent(Json.Serialize(new Dictionary<string, object> {
			{ "spell1Id", spell1Id },
			{ "spell2Id", spell2Id }
		}));
		request.Headers.ContentType.MediaType = "application/json";

		if ((await http.SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), "/lol-champ-select/v1/session/my-selection") {
			Content = request
		})).IsSuccessStatusCode) {
			Console.WriteLine("Summoner spells successfully updated");
		} else {
			Console.WriteLine("Failed to update summoner spells");
		}
	}

	static async Task PrintLolAlyticsData(LolAlytics lolAlyticsData) {
		if (lolAlyticsData is null) {
			return;
		}
		if (setSummonerSpells) {
			await UpdateSummonerSpells(lolAlyticsData.spell1Id, lolAlyticsData.spell2Id);
		}
		Console.WriteLine($"Skill order: {lolAlyticsData.skillOrder}");
		Console.WriteLine($"First skills: {lolAlyticsData.firstSkills}");
		Console.WriteLine();
	}
}
