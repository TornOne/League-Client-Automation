using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LCA {
	partial class LolAlytics {
		static readonly HttpClient http = new HttpClient() {
			BaseAddress = new Uri("https://ax.lolalytics.com")
		};
		static readonly string[] smallRunes = new[] { "5008", "5005", "5007", "5008f", "5002f", "5003f", "5001", "5002", "5003" };
		static readonly Dictionary<Lane, BanInfo[]> banSuggestions = new Dictionary<Lane, BanInfo[]>();
		static readonly Dictionary<Lane, Dictionary<int, RankInfo>> ranks = new Dictionary<Lane, Dictionary<int, RankInfo>>();

		public readonly string url, skillOrder, firstSkills;
		public readonly int spell1Id, spell2Id;
		public readonly RunePage runePage;
		public readonly ItemSet itemSet;

		LolAlytics(string url, string skillOrder, string firstSkills, int spell1Id, int spell2Id, RunePage runePage, ItemSet itemSet) {
			this.url = url;
			this.skillOrder = skillOrder;
			this.firstSkills = firstSkills;
			this.spell1Id = spell1Id;
			this.spell2Id = spell2Id;
			this.runePage = runePage;
			this.itemSet = itemSet;
		}

		public static async Task<BanInfo[]> GetBanSuggestions(Lane lane) {
			if (banSuggestions.TryGetValue(lane, out BanInfo[] topBans)) {
				return topBans;
			}

			try {
				Json.Node rankings = Json.Node.Parse(await http.GetStringAsync($"/tierlist/1/?{MakeQueryString(lane)}"));
				int allPicks = rankings["pick"].Get<int>();
				double avgWr = rankings["win"].Get<double>() / allPicks;

				List<BanInfo> bans = new List<BanInfo>();
				foreach (KeyValuePair<string, Json.Node> champion in (Json.Object)rankings["cid"]) {
					int picks = champion.Value[4].Get<int>();
					if (picks == 0) {
						continue;
					}
					//delta WR * pick rate / (1 - ban rate)
					bans.Add(new BanInfo(int.Parse(champion.Key), (champion.Value[3].Get<double>() / picks - avgWr) * picks / allPicks / (1 - champion.Value[lane <= Lane.Support ? 6 : 5].Get<double>() * 0.01) * 1e5));
				}
				bans.Sort((a, b) => Math.Sign(b.pbi - a.pbi));

				banSuggestions[lane] = new BanInfo[lane <= Lane.Support ? Config.banSuggestions : Config.eventBanSuggestions];
				bans.CopyTo(0, banSuggestions[lane], 0, banSuggestions[lane].Length);
			} catch (Exception e) {
				Console.WriteLine($"Fetching {lane} ranking data failed ({e.Message})\n{e.StackTrace}");
				banSuggestions[lane] = Array.Empty<BanInfo>();
			}

			return banSuggestions[lane];
		}

		public static async Task<Dictionary<int, RankInfo>> GetRanks(Lane queue) {
			if (LolAlytics.ranks.TryGetValue(queue, out Dictionary<int, RankInfo> ranks)) {
				return ranks;
			}

			ranks = new Dictionary<int, RankInfo>();
			try {
				Json.Node rankings = Json.Node.Parse(await http.GetStringAsync($"/tierlist/1/?{MakeQueryString(queue)}"));
				double avgWr = rankings["win"].Get<double>() / rankings["pick"].Get<int>();

				foreach (KeyValuePair<string, Json.Node> champion in (Json.Object)rankings["cid"]) {
					double wr = champion.Value[3].Get<double>() / champion.Value[4].Get<int>();
					ranks[int.Parse(champion.Key)] = new RankInfo(champion.Value[0].Get<int>(), wr, wr - avgWr);
				}
			} catch (Exception e) {
				Console.WriteLine($"Fetching {queue} ranking data failed ({e.Message})\n{e.StackTrace}");
			}

			LolAlytics.ranks[queue] = ranks;
			return ranks;
		}

		public static async Task<LolAlytics> FetchData(Lane lane, int championId) {
			try {
				//Fetch data
				string queryString = $"&p=d&v=1&cid={championId}&{MakeQueryString(lane)}";
				Json.Node data = Json.Node.Parse(await http.GetStringAsync("/mega/?ep=champion" + queryString));
				Json.Node data2 = Json.Node.Parse(await http.GetStringAsync("/mega/?ep=champion2" + queryString));
				Json.Node skills = data2["skills"];
				int pickTotal = data["n"].Get<int>();

				if (pickTotal < Config.minGamesChamp) {
					Console.WriteLine($"Insufficient amount of games ({pickTotal}) played. Try loading your champion manually.");
					return null;
				}

				//URL
				bool isMainGameMode = lane <= Lane.Support;
				string laneString = lane.ToString().ToLower();
				string url = $"https://lolalytics.com/lol/{Champion.idToChampion[championId].name}/{(isMainGameMode ? $"build/?lane={laneString}&" : $"{laneString}/build/?")}tier={Config.queueRankMap[(isMainGameMode ? Lane.Default : lane)]}&patch={Client.State.currentVersion}";

				//Skill order
				string bestSkillOrder = string.Empty;
				double bestValue = 0;
				foreach (Json.Array skillOrder in (Json.Array)skills["skillOrder"]) {
					double value = GetValue(pickTotal, skillOrder[1].Get<int>(), skillOrder[2].Get<int>());
					if (value > bestValue) {
						bestValue = value;
						bestSkillOrder = skillOrder[0].Get<string>();
					}
				}
				bestSkillOrder = string.Join(" > ", bestSkillOrder.ToCharArray());

				//First skills (PS. "skillOrder" exists too)
				string firstSkills = string.Empty;
				Dictionary<string, (int picks, int wins)> allSkills = new Dictionary<string, (int, int)>();
				foreach (Json.Array sixSkills in (Json.Array)skills["skill6"]) {
					string fiveSkills = sixSkills[0].Get<int>().ToString().Substring(0, 5);
					int picks = sixSkills[1].Get<int>();
					int wins = sixSkills[2].Get<int>();
					allSkills[fiveSkills] = allSkills.TryGetValue(fiveSkills, out (int picks, int wins) stats) ? (stats.picks + picks, stats.wins + wins) : (picks, wins);
				}
				for (int i = 0; i < 5; i++) {
					firstSkills += ChooseNextSkill(pickTotal, allSkills, firstSkills);
				}
				firstSkills = string.Join(" -> ", Array.ConvertAll(firstSkills.ToCharArray(), c => c == '1' ? 'Q' : c == '2' ? 'W' : c == '3' ? 'E' : 'R'));

				//Spells
				string spells = string.Empty;
				bestValue = 0;
				foreach (Json.Array spellsData in (Json.Array)data["spells"]) {
					double value = GetValue(pickTotal, spellsData[3].Get<int>(), GetWinCount(spellsData[3], spellsData[1]));
					if (value > bestValue) {
						bestValue = value;
						spells = spellsData[0].Get<string>();
					}
				}
				int[] bestSpells = Array.ConvertAll(spells.Split('_'), int.Parse);
				Array.Sort(bestSpells, (x, y) => Array.IndexOf(Config.spellOrder, (Spell)x) - Array.IndexOf(Config.spellOrder, (Spell)y));

				//Runes
				Json.Object runes = (Json.Object)data["runes"]["stats"];
				double GetVal(Json.Node counts) => GetValue(pickTotal, counts[2].Get<int>(), GetWinCount(counts[2], counts[1]));
				double[][] keystone = RunePage.KeystoneTemplate;
				double[,][] primary = RunePage.RuneTemplate;
				double[,][] secondary = RunePage.RuneTemplate;
				foreach (KeyValuePair<string, Json.Node> rune in runes) {
					if (Array.Exists(smallRunes, id => id == rune.Key)) {
						continue;
					}
					RunePage.Index index = RunePage.idToIndex[int.Parse(rune.Key)];
					Json.Array stats = (Json.Array)rune.Value;

					if (index.row == 0) { //Keystone
						keystone[index.category][index.column] = GetVal(stats[0]);
					} else {
						primary[index.category, index.row - 1][index.column] = GetVal(stats[0]);
					}

					if (stats.Count > 1) {
						secondary[index.category, index.row - 1][index.column] = GetVal(stats[1]);
					}
				}
				int[] bestRunes = RunePage.GetBestRunes(keystone, primary, secondary);

				for (int i = 0; i < 3; i++) {
					int bestRune = 0;
					bestValue = 0;
					for (int j = 0; j < 3; j++) {
						string runeId = smallRunes[i * 3 + j];
						double value = GetVal(runes[runeId][0]);
						if (value > bestValue) {
							bestValue = value;
							bestRune = int.Parse(runeId.Substring(0, 4));
						}
					}
					bestRunes[8 + i] = bestRune;
				}

				return new LolAlytics(url, bestSkillOrder, firstSkills, bestSpells[0], bestSpells[1], new RunePage(bestRunes), new ItemSet(championId, lane, data, data2));
			} catch (Exception e) {
				Console.WriteLine($"Fetching LolAlytics data failed ({e.Message})\n{e.StackTrace}");
				return null;
			}
		}

		#region Helper methods
		static int GetWinCount(Json.Node pickCount, Json.Node winChance) => Convert.ToInt32(pickCount.Get<int>() * winChance.Get<double>() * 0.01);
		// (pick chance) / (127/128 * pick chance + 1/128) * win chance	// Laplace smoothing, sort of
		static double GetValue(int pickTotal, int pickCount, int winCount) => (double)(128 * winCount) / (127 * pickCount + pickTotal);
		//static double GetValue(int pickTotal, int pickCount, double winChance) => (128 * pickCount * winChance) / (127 * pickCount + pickTotal);
		//static double GetValue(double pickChance, double winChance) => (128 * pickChance * winChance) / (127 * pickChance + 1);

		static string MakeQueryString(Lane lane) {
			bool isMainGameMode = lane <= Lane.Support;
			Lane queue = isMainGameMode ? Lane.Default : lane;
			return $"lane={(isMainGameMode ? lane.ToString().ToLower() : "default")}&patch={Client.State.currentVersion}&tier={Config.queueRankMap[queue]}&queue={(queue == Lane.Default ? 420 : (int)queue)}&region=all";
		}

		static char ChooseNextSkill(int pickTotal, Dictionary<string, (int picks, int wins)> allSkills, string given) {
			Dictionary<char, (int picks, int wins)> nextSkills = new Dictionary<char, (int, int)>() {
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
		#endregion
	}
}