using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Torn.Json;

namespace LCA.ThirdParty;
class LolAlytics : IInterface {
	static readonly HttpClient http = new() {
		BaseAddress = new Uri("https://ax.lolalytics.com")
	};
	static readonly string[] smallRunes = [ "5008", "5005", "5007", "5008f", "5002f", "5003f", "5001", "5002", "5003" ];
	static readonly Dictionary<Lane, BanInfo[]> banSuggestions = [];
	static readonly Dictionary<Lane, Dictionary<int, RankInfo>> ranks = [];

	//TODO: Might need to check at the start, whether there are enough games played this patch to even fetch ban suggestions and the like
	public static Task Initialize() => Task.CompletedTask;

	public static async Task<BanInfo[]> GetBanSuggestions(Lane lane) {
		if (banSuggestions.TryGetValue(lane, out BanInfo[]? topBans)) {
			return topBans;
		}

		try {
			using JsonDocument jsonDoc = JsonDocument.Parse(await http.GetStringAsync($"/tierlist/1/?{MakeQueryString(lane)}"));
			JsonElement rankings = jsonDoc.RootElement;
			int allPicks = rankings.GetProperty("pick").GetInt32();
			double avgWr = rankings.GetProperty("win").GetDouble() / allPicks;

			List<BanInfo> bans = [];
			foreach (JsonProperty champion in rankings.GetProperty("cid").EnumerateObject()) {
				int picks = champion.Value[4].GetInt32();
				if (picks == 0) {
					continue;
				}
				//delta WR * pick rate / (1 - ban rate)
				bans.Add(new BanInfo(int.Parse(champion.Name), (champion.Value[3].GetDouble() / picks - avgWr) * picks / allPicks / (1 - champion.Value[lane <= Lane.Support ? 6 : 5].GetDouble() * 0.01) * 1e5));
			}
			bans.Sort((a, b) => Math.Sign(b.pbi - a.pbi));

			banSuggestions[lane] = new BanInfo[lane <= Lane.Support ? Config.banSuggestions : Config.eventBanSuggestions];
			bans.CopyTo(0, banSuggestions[lane], 0, banSuggestions[lane].Length);
		} catch (Exception e) {
			Console.WriteLine($"Fetching {lane} ranking data failed ({e.Message})\n{e.StackTrace}");
			banSuggestions[lane] = [];
		}

		return banSuggestions[lane];
	}

	public static async Task<Dictionary<int, RankInfo>> GetRanks(Lane queue) {
		if (LolAlytics.ranks.TryGetValue(queue, out Dictionary<int, RankInfo>? ranks)) {
			return ranks;
		}

		ranks = [];
		try {
			using JsonDocument jsonDoc = JsonDocument.Parse(await http.GetStringAsync($"/tierlist/1/?{MakeQueryString(queue)}"));
			JsonElement rankings = jsonDoc.RootElement;
			double avgWr = rankings.GetProperty("win").GetDouble() / rankings.GetProperty("pick").GetInt32();

			foreach (JsonProperty champion in rankings.GetProperty("cid").EnumerateObject()) {
				double wr = champion.Value[3].GetDouble() / champion.Value[4].GetInt32();
				ranks[int.Parse(champion.Name)] = new RankInfo(champion.Value[0].GetInt32(), wr, wr - avgWr);
			}
		} catch (Exception e) {
			Console.WriteLine($"Fetching {queue} ranking data failed ({e.Message})\n{e.StackTrace}");
		}

		LolAlytics.ranks[queue] = ranks;
		return ranks;
	}

	public static Task<Data?> FetchData(Lane lane, int championId) => FetchData(lane, championId, false);
	static async Task<Data?> FetchData(Lane lane, int championId, bool previousPatch) {
		try {
			//Fetch data
			string queryString = $"&p=d&v=1&cid={championId}&{MakeQueryString(lane, previousPatch)}";
			using JsonDocument jsonDoc = JsonDocument.Parse(await http.GetStringAsync("/mega/?ep=champion" + queryString));
			using JsonDocument jsonDoc2 = JsonDocument.Parse(await http.GetStringAsync("/mega/?ep=champion2" + queryString));
			JsonElement data = jsonDoc.RootElement;
			JsonElement data2 = jsonDoc2.RootElement;
			JsonElement skills = data2.GetProperty("skills");
			int pickTotal = data.GetProperty("n").GetInt32();

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

			//URL
			bool isMainGameMode = lane <= Lane.Support;
			string laneString = lane.ToString().ToLower();
			string url = $"https://lolalytics.com/lol/{Champion.idToChampion[championId].name}/{(isMainGameMode ? $"build/?lane={laneString}&" : $"{laneString}/build/?")}tier={Config.lolAlyticsQueueRankMap[(isMainGameMode ? Lane.Default : lane)]}&patch={Client.State.gameVersionMajor}.{Client.State.gameVersionMinor}";

			//Skill order
			string bestSkillOrder = string.Empty;
			double bestValue = 0;
			foreach (JsonElement skillOrder in skills.GetProperty("skillOrder").EnumerateArray()) {
				double value = GetValue(pickTotal, skillOrder[1].GetInt32(), skillOrder[2].GetInt32());
				if (value > bestValue) {
					bestValue = value;
					bestSkillOrder = skillOrder[0].GetString()!;
				}
			}
			bestSkillOrder = string.Join(" > ", bestSkillOrder.ToCharArray());

			//First skills (PS. "skillOrder" exists too)
			string firstSkills = string.Empty;
			Dictionary<string, (int picks, int wins)> allSkills = [];
			foreach (JsonElement sixSkills in skills.GetProperty("skill6").EnumerateArray()) {
				string fiveSkills = sixSkills[0].GetInt32().ToString()[..5];
				int picks = sixSkills[1].GetInt32();
				int wins = sixSkills[2].GetInt32();
				allSkills[fiveSkills] = allSkills.TryGetValue(fiveSkills, out (int picks, int wins) stats) ? (stats.picks + picks, stats.wins + wins) : (picks, wins);
			}
			for (int i = 0; i < 5; i++) {
				firstSkills += ChooseNextSkill(pickTotal, allSkills, firstSkills);
			}
			firstSkills = string.Join(" -> ", Array.ConvertAll(firstSkills.ToCharArray(), c => c == '1' ? 'Q' : c == '2' ? 'W' : c == '3' ? 'E' : 'R'));

			//Spells
			string spells = string.Empty;
			bestValue = 0;
			foreach (JsonElement spellsData in data.GetProperty("spells").EnumerateArray()) {
				double value = GetValue(pickTotal, spellsData[3].GetInt32(), GetWinCount(spellsData[3], spellsData[1]));
				if (value > bestValue) {
					bestValue = value;
					spells = spellsData[0].GetString()!;
				}
			}
			int[] bestSpells = Array.ConvertAll(spells.Split('_'), int.Parse);
			Array.Sort(bestSpells, (x, y) => Array.IndexOf(Config.spellOrder, (Spell)x) - Array.IndexOf(Config.spellOrder, (Spell)y));

			//Runes
			JsonElement runes = data.GetProperty("runes").GetProperty("stats");
			double GetVal(JsonElement counts) => GetValue(pickTotal, counts[2].GetInt32(), GetWinCount(counts[2], counts[1]));
			double[][] keystone = RunePage.KeystoneTemplate;
			double[,][] primary = RunePage.RuneTemplate;
			double[,][] secondary = RunePage.RuneTemplate;
			foreach (JsonProperty rune in runes.EnumerateObject()) {
				if (Array.Exists(smallRunes, id => id == rune.Name)) {
					continue;
				}
				RunePage.Index index = RunePage.idToIndex[int.Parse(rune.Name)];
				JsonElement stats = rune.Value;

				if (index.row == 0) { //Keystone
					keystone[index.category][index.column] = GetVal(stats[0]);
				} else {
					primary[index.category, index.row - 1][index.column] = GetVal(stats[0]);
				}

				if (stats.GetArrayLength() > 1) {
					secondary[index.category, index.row - 1][index.column] = GetVal(stats[1]);
				}
			}
			int[] bestRunes = RunePage.GetBestRunes(keystone, primary, secondary);

			for (int i = 0; i < 3; i++) {
				int bestRune = 0;
				bestValue = 0;
				for (int j = 0; j < 3; j++) {
					string runeId = smallRunes[i * 3 + j];
					double value = GetVal(runes.GetProperty(runeId)[0]);
					if (value > bestValue) {
						bestValue = value;
						bestRune = int.Parse(runeId[..4]);
					}
				}
				bestRunes[8 + i] = bestRune;
			}

			//Items
			List<ItemSet.Block> blocks = new(10) {
				SelectBestItems("Start", data2.GetProperty("startSet"), 4)
			};
			for (int i = 1; i <= 5; i++) {
				if (data.TryGetValue($"item{i}", out JsonElement items)) {
					blocks.Add(SelectBestItems($"Item {i}", items, 6));
				}
			}
			JsonElement allItems = data.GetProperty("popularItem");
			blocks.Add(SelectBestItems("Overall", allItems, 12));
			blocks.Add(SelectSituationalItems(allItems, 12));

			#region Boots
			if (championId != 69 && championId != 350) { //Cassiopeia and Yuumi don't use boots
				HashSet<string> possibleBoots = [];
				foreach (JsonElement boot in data.GetProperty("boots").EnumerateArray()) {
					possibleBoots.Add(boot[0].GetInt32().ToString());
				}
				foreach (string id in new[] { "9999", "1001", "2422" }) { //No boots, basic Boots, Magical Footwear
					possibleBoots.Remove(id);
				}
				int bootsPickedFirst = 0;
				int bootsWonFirst = 0;
				int bootsPickedSecond = 0;
				int bootsWonSecond = 0;
				for (int i = 2; i <= 6; i++) {
					if (!data2.GetProperty("itemSets").TryGetValue($"itemBootSet{i}", out JsonElement itemBootSet)) {
						continue;
					}
					foreach (JsonProperty pair in itemBootSet.EnumerateObject()) {
						string[] items = pair.Name.Split('_');
						int picks = pair.Value[0].GetInt32();
						int wins = pair.Value[1].GetInt32();
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

				blocks.Insert(bootsDiff > 0 ? 1 : 2, SelectBestItems($"Boots +{Math.Abs(bootsDiff):0%} {(bootsDiff > 0 ? "1st" : "2nd")}", data.GetProperty("boots"), 6));
			}
			#endregion

			return new Data(url, bestSkillOrder, firstSkills, bestSpells[0], bestSpells[1], new RunePage(bestRunes), new ItemSet(championId, lane, [.. blocks]));
		} catch (Exception e) {
			Console.WriteLine($"Fetching LolAlytics data failed ({e.Message})\n{e.StackTrace}");
			return null;
		}
	}

	#region Helper methods
	static int GetWinCount(JsonElement pickCount, JsonElement winChance) => Convert.ToInt32(pickCount.GetInt32() * winChance.GetDouble() * 0.01);
	// (pick chance) / (127/128 * pick chance + 1/128) * win chance	// Laplace smoothing, sort of
	static double GetValue(int pickTotal, int pickCount, int winCount) => (double)(128 * winCount) / (127 * pickCount + pickTotal);
	//static double GetValue(int pickTotal, int pickCount, double winChance) => (128 * pickCount * winChance) / (127 * pickCount + pickTotal);
	//static double GetValue(double pickChance, double winChance) => (128 * pickChance * winChance) / (127 * pickChance + 1);

	static string MakeQueryString(Lane lane, bool previousPatch = false) {
		bool isMainGameMode = lane <= Lane.Support;
		Lane queue = isMainGameMode ? Lane.Default : lane;
		return $"lane={(isMainGameMode ? lane.ToString().ToLower() : "default")}&patch={Client.State.gameVersionMajor}.{(previousPatch ? Client.State.gameVersionMinor - 1 : Client.State.gameVersionMinor)}&tier={Config.lolAlyticsQueueRankMap[queue]}&queue={(queue == Lane.Default ? 420 : (int)queue)}&region=all";
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
	static ItemSet.Block SelectBestItems(string nameBase, JsonElement itemsJson, int maxItems) {
		static double SmoothPR(double pickRate) => 128 * pickRate / (127 * pickRate + 1);

		double maxPickRate = itemsJson[0][2].GetDouble() * 0.01;
		List<(int[] ids, double value, double pr)> items = new(itemsJson.GetArrayLength());
		foreach (JsonElement item in itemsJson.EnumerateArray()) {
			int[] ids;
			if (item[0].TryGetValue(out int id)) {
				if (id == 3041) { //Do not suggest Mejai - it is far too large an outlier
					continue;
				}
				ids = [id];
			} else {
				string idString = item[0].GetString()!;
				ids = idString == string.Empty ? [] : Array.ConvertAll(idString.Split('_'), int.Parse);
			}
			double pr = item[2].GetDouble() * 0.01;
			items.Add((ids, SmoothPR(pr) * item[1].GetDouble() * 0.01, pr));
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

	static ItemSet.Block SelectSituationalItems(JsonElement itemsJson, int maxItems) {
		static double SmoothPR(double pickRate) => 1024 * pickRate / (1023 * pickRate + 1);

		List<(int id, double value)> items = new(itemsJson.GetArrayLength());
		foreach (JsonElement item in itemsJson.EnumerateArray()) {
			int id = item[0].GetInt32();
			items.Add((id, SmoothPR(item[2].GetDouble() * 0.01) * item[1].GetDouble() * 0.01));
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
