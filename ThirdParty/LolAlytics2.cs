using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LCA.ThirdParty;
//I hate this rework of LoLalytics and whatever the fuck a qwik is
//There exist accessible endpoints for the underlying data, but they are usually not used by the frontend, leaving their names a mystery
//Here are some that I've found, with example query strings (the whole query may not be useful) (a1, a2, and a3 probably all work, and I don't know what the difference is):
//https://a1.lolalytics.com/mega/?ep=front&p=d&v=1&patch=14.3&cid=20&lane=jungle&tier=emerald_plus&queue=420&region=all Front (landing) page data
//https://a1.lolalytics.com/mega/?ep=list&v=1&patch=14.3&lane=jungle&tier=emerald_plus&queue=aram&region=all Tierlist data
//https://a1.lolalytics.com/mega/?ep=build-team&v=1&patch=13.21&c=nunu&lane=middle&tier=bronze&queue=ranked&region=all Info on teammates, synergies, counters, and the like
//https://a1.lolalytics.com/mega/?ep=build-earlyset&v=1&patch=13.21&c=nunu&lane=middle&tier=bronze&queue=ranked&region=all Early item sets?
//https://a1.lolalytics.com/mega/?ep=build-itemset&v=1&patch=13.21&c=nunu&lane=middle&tier=bronze&queue=ranked&region=all Other item sets?
class LolAlytics2 : IInterface {
	static readonly HttpClient http = new();
	static readonly string[] smallRunes = [ "5008", "5005", "5007", "5008f", "5010f", "5001f", "5011", "5013", "5001" ];
	static readonly Dictionary<Lane, BanInfo[]> banSuggestions = [];
	static readonly Dictionary<Lane, Dictionary<int, RankInfo>> ranks = [];

	//TODO: Might need to check at the start, whether there are enough games played this patch to even fetch ban suggestions and the like
	public static Task Initialize() => Task.CompletedTask;

	public static async Task<BanInfo[]> GetBanSuggestions(Lane lane) {
		if (banSuggestions.TryGetValue(lane, out BanInfo[]? topBans)) {
			return topBans;
		}

		bool isMainGameMode = lane <= Lane.Support;
		try {
			using JsonDocument jsonDoc = JsonDocument.Parse(await http.GetStringAsync($"https://a1.lolalytics.com/mega/{MakeListQueryString(lane)}"));
			JsonElement data = jsonDoc.RootElement;
			int allPicks = data.GetProperty("analysed").GetInt32();
			double avgWr = data.GetProperty("avgWr").GetDouble() * 0.01;

			List<BanInfo> bans = [];
			foreach (JsonProperty championProp in data.GetProperty("cid").EnumerateObject()) {
				JsonElement champion = championProp.Value;
				int picks = champion.GetProperty("games").GetInt32();
				if (picks == 0) {
					continue;
				}
				//delta WR * pick rate / (1 - ban rate)
				bans.Add(new BanInfo(int.Parse(championProp.Name), (champion.GetProperty("wr").GetDouble() * 0.01 - avgWr) * picks / allPicks / (1 - champion.GetProperty("br").GetDouble() * 0.01) * 1e5));
			}
			bans.Sort((a, b) => Math.Sign(b.pbi - a.pbi));

			banSuggestions[lane] = new BanInfo[isMainGameMode ? Config.banSuggestions : Config.eventBanSuggestions];
			bans.CopyTo(0, banSuggestions[lane], 0, banSuggestions[lane].Length);
		} catch (Exception e) {
			Console.WriteLine($"Fetching {lane} ranking data failed ({e.Message})\n{e.StackTrace}");
			banSuggestions[lane] = [];
		}

		return banSuggestions[lane];
	}

	public static async Task<Dictionary<int, RankInfo>> GetRanks(Lane queue) {
		if (LolAlytics2.ranks.TryGetValue(queue, out Dictionary<int, RankInfo>? ranks)) {
			return ranks;
		}

		ranks = [];
		try {
			using JsonDocument jsonDoc = JsonDocument.Parse(await http.GetStringAsync($"https://a1.lolalytics.com/mega/{MakeListQueryString(queue)}"));
			JsonElement data = jsonDoc.RootElement;
			double avgWr = data.GetProperty("avgWr").GetDouble() * 0.01;

			foreach (JsonProperty championProp in data.GetProperty("cid").EnumerateObject()) {
				JsonElement champion = championProp.Value;
				double wr = champion.GetProperty("wr").GetDouble() * 0.01;
				ranks[int.Parse(championProp.Name)] = new RankInfo(champion.GetProperty("rank").GetInt32(), wr, wr - avgWr);
			}
		} catch (Exception e) {
			Console.WriteLine($"Fetching {queue} ranking data failed ({e.Message})\n{e.StackTrace}");
		}

		LolAlytics2.ranks[queue] = ranks;
		return ranks;
	}

	public static Task<Data?> FetchData(Lane lane, int championId) => FetchData(lane, championId, false);
	static async Task<Data?> FetchData(Lane lane, int championId, bool previousPatch) {
		try {
			//Fetch data
			bool isMainGameMode = lane <= Lane.Support;
			string laneString = lane.ToString().ToLower();
			string baseUrl = $"https://lolalytics.com/lol/{Champion.idToChampion[championId].name}/{(isMainGameMode ? "" : $"{laneString}/")}build/";
			string queryString = MakeQueryString(lane, previousPatch);
			Dictionary<string, object> jsonObj;
			using (JsonDocument jsonDoc = JsonDocument.Parse(await http.GetStringAsync(baseUrl + "q-data.json" + queryString))) {
				JsonElement json = jsonDoc.RootElement;
				JsonElement objs = json.GetProperty("_objs");
				jsonObj = (Dictionary<string, object>)ResolveRefs(objs, Deref(objs, json.GetProperty("_entry")));
			}

			string[] requiredKeys = [ "n", "skillOrder", "skill6", "spells", "runes", "startSet", "popularItem", "boots", "itemSets" ];
			Dictionary<string, object>? data = null;
			foreach (object loader in ((Dictionary<string, object>)jsonObj["loaders"]).Values) {
				Dictionary<string, object> potentialData = (Dictionary<string, object>)loader;
				bool suitable = true;
				foreach (string key in requiredKeys) {
					if (!potentialData.ContainsKey(key)) {
						suitable = false;
						break;
					}
				}
				if (suitable) {
					data = potentialData;
				}
			}
			if (data is null) {
				throw new Exception("Failed to find all required keys in json data");
			}
			
			int pickTotal = (int)data["n"];
			if (pickTotal < Config.minGamesChamp) {
				if (previousPatch) {
					Console.WriteLine($"Still an insufficient amount of games ({pickTotal}) played. Try loading your champion manually.");
					return null;
				}

				Console.Write($"Insufficient amount of games ({pickTotal}) played this patch, ");
				if (Client.State.gameVersionMinor <= 1) {
					Console.WriteLine("try loading your champion manually.");
					return null;
				}
				Console.WriteLine("trying previous patch.");
				return await FetchData(lane, championId, true);
			}

			//Skill order
			string bestSkillOrder = string.Empty;
			double bestValue = 0;
			foreach (object skillOrderObj in (List<object>)data["skillOrder"]) {
				List<object> skillOrder = (List<object>)skillOrderObj;
				int picks = (int)skillOrder[3];
				double value = GetValue(pickTotal, picks, GetWinCount(picks, skillOrder[1]));
				if (value > bestValue) {
					bestValue = value;
					bestSkillOrder = (string)skillOrder[0];
				}
			}
			bestSkillOrder = string.Join(" > ", bestSkillOrder.ToCharArray());

			//First skills
			string firstSkills = string.Empty;
			Dictionary<string, (int picks, int wins)> allSkills = [];
			foreach (object sixSkillsObj in (List<object>)data["skill6"]) {
				List<object> sixSkills = (List<object>)sixSkillsObj;
				string fiveSkills = sixSkills[0].ToString()![..5];
				int picks = (int)sixSkills[3];
				int wins = GetWinCount(picks, sixSkills[1]);
				allSkills[fiveSkills] = allSkills.TryGetValue(fiveSkills, out (int picks, int wins) stats) ? (stats.picks + picks, stats.wins + wins) : (picks, wins);
			}
			for (int i = 0; i < 5; i++) {
				firstSkills += ChooseNextSkill(pickTotal, allSkills, firstSkills);
			}
			firstSkills = string.Join(" -> ", Array.ConvertAll(firstSkills.ToCharArray(), c => c == '1' ? 'Q' : c == '2' ? 'W' : c == '3' ? 'E' : 'R'));

			//Spells
			string spells = string.Empty;
			bestValue = 0;
			foreach (object spellsDataObj in (List<object>)data["spells"]) {
				List<object> spellsData = (List<object>)spellsDataObj;
				int picks = (int)spellsData[3];
				double value = GetValue(pickTotal, picks, GetWinCount(picks, spellsData[1]));
				if (value > bestValue) {
					bestValue = value;
					spells = (string)spellsData[0];
				}
			}
			int[] bestSpells = Array.ConvertAll(spells.Split('_'), int.Parse);
			Array.Sort(bestSpells, (x, y) => Array.IndexOf(Config.spellOrder, (Spell)x) - Array.IndexOf(Config.spellOrder, (Spell)y));

			//Runes
			Dictionary<string, object> runes = (Dictionary<string, object>)((Dictionary<string, object>)data["runes"])["stats"];
			double GetVal(List<object> counts) => GetValue(pickTotal, (int)counts[2], GetWinCount((int)counts[2], counts[1]));
			double[][] keystone = RunePage.KeystoneTemplate;
			double[,][] primary = RunePage.RuneTemplate;
			double[,][] secondary = RunePage.RuneTemplate;
			foreach (KeyValuePair<string, object> rune in runes) {
				if (Array.Exists(smallRunes, id => id == rune.Key)) {
					continue;
				}
				RunePage.Index index = RunePage.idToIndex[int.Parse(rune.Key)];
				List<object> stats = (List<object>)rune.Value;

				if (index.row == 0) { //Keystone
					keystone[index.category][index.column] = GetVal((List<object>)stats[0]);
				} else {
					primary[index.category, index.row - 1][index.column] = GetVal((List<object>)stats[0]);
				}

				if (stats.Count > 1) {
					secondary[index.category, index.row - 1][index.column] = GetVal((List<object>)stats[1]);
				}
			}
			int[] bestRunes = RunePage.GetBestRunes(keystone, primary, secondary);

			for (int i = 0; i < 3; i++) {
				int bestRune = 0;
				bestValue = 0;
				for (int j = 0; j < 3; j++) {
					string runeId = smallRunes[i * 3 + j];
					double value = GetVal((List<object>)((List<object>)runes[runeId])[0]);
					if (value > bestValue) {
						bestValue = value;
						bestRune = int.Parse(runeId[..4]);
					}
				}
				bestRunes[8 + i] = bestRune;
			}

			//Items
			List<ItemSet.Block> blocks = new(10) {
				SelectBestItems("Start", (List<object>)data["startSet"], 4)
			};
			for (int i = 1; i <= 5; i++) {
				if (data.TryGetValue($"item{i}", out object? items)) {
					blocks.Add(SelectBestItems($"Item {i}", (List<object>)items, 6));
				}
			}
			List<object> allItems = (List<object>)data["popularItem"];
			blocks.Add(SelectBestItems("Overall", allItems, 12));
			blocks.Add(SelectSituationalItems(allItems, 12));

			#region Boots
			if (championId != 69 && championId != 350) { //Cassiopeia and Yuumi don't use boots
				HashSet<string> possibleBoots = [];
				foreach (object boot in (List<object>)data["boots"]) {
					possibleBoots.Add(((List<object>)boot)[0].ToString()!);
				}
				foreach (string id in new[] { "9999", "1001", "2422" }) { //No boots, basic Boots, Magical Footwear
					possibleBoots.Remove(id);
				}
				int bootsPickedFirst = 0;
				int bootsWonFirst = 0;
				int bootsPickedSecond = 0;
				int bootsWonSecond = 0;
				for (int i = 2; i <= 6; i++) {
					if (!((Dictionary<string, object>)data["itemSets"]).TryGetValue($"itemBootSet{i}", out object? itemBootSet)) {
						continue;
					}
					foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)itemBootSet) {
						string[] items = pair.Key.Split('_');
						List<object> counts = (List<object>)pair.Value;
						int picks = (int)counts[0];
						int wins = (int)counts[1];
						if (possibleBoots.Contains(items[0])) {
							bootsPickedFirst += picks;
							bootsWonFirst += wins;
						} else if (possibleBoots.Contains(items[1])) {
							bootsPickedSecond += picks;
							bootsWonSecond += wins;
						}
					}
				}
				double bootsDiff = (double)(128 * bootsWonFirst) / (128 * bootsPickedFirst + bootsPickedSecond) - (double)(128 * bootsWonSecond) / (128 * bootsPickedSecond + bootsPickedFirst);

				blocks.Insert(bootsDiff > 0 ? 1 : 2, SelectBestItems($"Boots +{Math.Abs(bootsDiff):0%} {(bootsDiff > 0 ? "1st" : "2nd")}", (List<object>)data["boots"], 6));
			}
			#endregion

			return new Data(baseUrl + queryString, bestSkillOrder, firstSkills, bestSpells[0], bestSpells[1], new RunePage(bestRunes), new ItemSet(championId, lane, [.. blocks]));
		} catch (Exception e) {
			Console.WriteLine($"Fetching LolAlytics data failed ({e.Message})\n{e.StackTrace}");
			return null;
		}
	}

	#region Helper methods
	static int GetWinCount(int pickCount, object winChance) => Convert.ToInt32(pickCount * (winChance as double? ?? (int)winChance) * 0.01);
	// (pick chance) / (127/128 * pick chance + 1/128) * win chance	// Laplace smoothing, sort of
	static double GetValue(int pickTotal, int pickCount, int winCount) => (double)(128 * winCount) / (127 * pickCount + pickTotal);
	//static double GetValue(int pickTotal, int pickCount, double winChance) => (128 * pickCount * winChance) / (127 * pickCount + pickTotal);
	//static double GetValue(double pickChance, double winChance) => (128 * pickChance * winChance) / (127 * pickChance + 1);

	static string MakeQueryString(Lane lane, bool previousPatch = false) {
		bool isMainGameMode = lane <= Lane.Support;
		return $"?{(isMainGameMode ? $"lane={lane.ToString().ToLower()}" : "")}&tier={Config.lolAlyticsQueueRankMap[(isMainGameMode ? Lane.Default : lane)]}&patch={Client.State.gameVersionMajor}.{(previousPatch ? Client.State.gameVersionMinor - 1 : Client.State.gameVersionMinor)}";
	}

	static string MakeListQueryString(Lane lane, bool previousPatch = false) {
		bool isMainGameMode = lane <= Lane.Support;
		string laneString = lane.ToString().ToLower();
		return $"?ep=list&v=1&patch={Client.State.gameVersionMajor}.{(previousPatch ? Client.State.gameVersionMinor - 1 : Client.State.gameVersionMinor)}{(isMainGameMode ? $"lane={laneString}" : "")}&tier={Config.lolAlyticsQueueRankMap[(isMainGameMode ? Lane.Default : lane)]}&queue={(isMainGameMode ? "ranked" : laneString)}&region=all";
	}

	static char ChooseNextSkill(int pickTotal, Dictionary<string, (int picks, int wins)> allSkills, string given) {
		Dictionary<char, (int picks, int wins)> nextSkills = new() {
			{ '1', (0, 0) },
			{ '2', (0, 0) },
			{ '3', (0, 0) },
			{ '4', (0, 0) }
		};

		foreach (KeyValuePair<string, (int picks, int wins)> skillSet in allSkills) {
			if (skillSet.Key.StartsWith(given)) {
				char skill = skillSet.Key[given.Length];
				(int picks, int wins) = nextSkills[skill];
				nextSkills[skill] = (picks + skillSet.Value.picks, wins + skillSet.Value.wins);
			}
		}

		char bestSkill = '?';
		double bestValue = 0;
		foreach (KeyValuePair<char, (int picks, int wins)> skill in nextSkills) {
			double value = GetValue(pickTotal, skill.Value.picks, skill.Value.wins);
			if (value > bestValue) {
				bestValue = value;
				bestSkill = skill.Key;
			}
		}

		return bestSkill;
	}

	//Select all items that have at least some % of the value of the best item
	static ItemSet.Block SelectBestItems(string nameBase, List<object> itemList, int maxItems) {
		static double SmoothPR(double pickRate) => 128 * pickRate / (127 * pickRate + 1);

		object maxPickRateObj = ((List<object>)itemList[0])[2];
		double maxPickRate = (maxPickRateObj as double? ?? (int)maxPickRateObj) * 0.01;
		List<(int[] ids, double value, double pr)> items = new(itemList.Count);
		foreach (object itemObj in itemList) {
			List<object> item = (List<object>)itemObj;
			int[] ids;
			if (item[0] is int id) {
				if (id == 3041) { //Do not suggest Mejai - it is far too large an outlier
					continue;
				}
				ids = [id];
			} else {
				string idString = (string)item[0];
				ids = idString == string.Empty ? [] : Array.ConvertAll(idString.Split('_'), int.Parse);
			}
			double pr = (item[2] as double? ?? (int)item[2]) * 0.01;
			items.Add((ids, SmoothPR(pr) * (item[1] as double? ?? (int)item[1]) * 0.01, pr));
		}
		items.Sort((a, b) => Math.Sign(b.value - a.value));

		StringBuilder name = new(nameBase);
		name.Append(" (");
		List<int> bestItems = new(maxItems);
		double highestValue = items[0].value;
		for (int i = 0; i < items.Count; i++) {
			(int[] ids, double value, double pr) = items[i];
			if (value < highestValue * (Math.Sqrt(48d / maxItems * i + 1) + 1) / 8) {
				break;
			}
			bestItems.AddRange(ids);
			double prDiff = Math.Log(maxPickRate / pr, 2);
			string suffix = prDiff < 0.5 ? "" :
				prDiff < 1.5 ? "¹" :
				prDiff < 2.5 ? "²" :
				"³";
			name.Append($"{(value - 0.5) * 100:0.0}{suffix}, ");
		}
		name.Remove(name.Length - 2, 2);
		name.Append(')');

		return new ItemSet.Block(name.ToString(), [.. bestItems]);
	}

	static ItemSet.Block SelectSituationalItems(List<object> itemList, int maxItems) {
		static double SmoothPR(double pickRate) => 1024 * pickRate / (1023 * pickRate + 1);

		List<(int id, double value)> items = new(itemList.Count);
		foreach (object itemObj in itemList) {
			List<object> item = (List<object>)itemObj;
			int id = (int)item[0];
			items.Add((id, SmoothPR((item[2] as double? ?? (int)item[2]) * 0.01) * (item[1] as double? ?? (int)item[1]) * 0.01));
		}
		items.Sort((a, b) => Math.Sign(b.value - a.value));

		StringBuilder name = new("Situational (");
		List<int> bestItems = new(maxItems);
		double highestValue = items[0].value;
		for (int i = 0; i < items.Count; i++) {
			(int id, double value) = items[i];
			if (value < highestValue * (Math.Sqrt(48d / maxItems * i + 1) + 1) / 8) {
				break;
			}
			bestItems.Add(id);
			name.Append($"{(value - 0.55) * 100:0.0}, ");
		}
		name.Remove(name.Length - 2, 2);
		name.Append(')');

		return new ItemSet.Block(name.ToString(), [.. bestItems]);
	}

	static object ResolveRefs(JsonElement objs, JsonElement element) {
		if (element.ValueKind == JsonValueKind.Object) {
			Dictionary<string, object> obj = [];
			foreach (JsonProperty property in element.EnumerateObject()) {
				obj[property.Name] = ResolveRefs(objs, Deref(objs, property.Value));
			}
			return obj;
		} else if (element.ValueKind == JsonValueKind.Array) {
			List<object> arr = [];
			foreach (JsonElement arrayElement in element.EnumerateArray()) {
				arr.Add(ResolveRefs(objs, Deref(objs, arrayElement)));
			}
			return arr;
		} else {
			return element.ValueKind == JsonValueKind.String ? element.GetString()! :
				element.ValueKind == JsonValueKind.Number ? element.TryGetInt32(out int value) ? (object)value : element.GetDouble() :
				element.ValueKind == JsonValueKind.True ? true :
				element.ValueKind == JsonValueKind.False ? false :
				null!;
		}
	}

	static JsonElement Deref(JsonElement objs, JsonElement reference) => objs[FromBase36(reference.GetString()!)];

	static int FromBase36(string base36) {
		int value = 0;
		foreach (char c in base36) {
			value = value * 36 + c - (c <= '9' ? '0' : 'W');
		}
		return value;
	}
	#endregion

	public enum Rank {
		unranked,
		iron,
		bronze,
		silver,
		gold,
		platinum,
		emerald,
		diamond,
		master,
		grandmaster,
		challenger,
		all,
		gold_plus,
		platinum_plus,
		emerald_plus,
		diamond_plus,
		d2_plus,
		master_plus
	}
}
