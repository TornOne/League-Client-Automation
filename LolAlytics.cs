using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LCA {
	class LolAlytics {
		static readonly HttpClient http = new HttpClient() {
			BaseAddress = new Uri("https://axe.lolalytics.com")
		};
		static readonly string[] smallRunes = new[] { "5008", "5005", "5007", "5008f", "5002f", "5003f", "5001", "5002", "5003" };
		public static Dictionary<Lane, (int id, double pbi)[]> banSuggestions = new Dictionary<Lane, (int, double)[]>();
		public static Dictionary<int, (int rank, double wr, double delta)> aramRanks;

		public readonly string url, skillOrder, firstSkills;
		public readonly int spell1Id, spell2Id;
		public readonly RunePage runePage;

		LolAlytics(string url, string skillOrder, string firstSkills, int spell1Id, int spell2Id, RunePage runePage) {
			this.url = url;
			this.skillOrder = skillOrder;
			this.firstSkills = firstSkills;
			this.spell1Id = spell1Id;
			this.spell2Id = spell2Id;
			this.runePage = runePage;
		}

		public static async Task FetchBanChoices(Lane lane) {
			try {
				Json.Node rankings = Json.Node.Parse(await http.GetStringAsync($"/tierlist/1/?lane={lane.ToString().ToLower()}&patch={Client.State.currentVersion}&tier={Config.queueRankMap[Lane.Default]}&queue=420&region=all"));
				int allPicks = rankings["pick"].Get<int>();
				double avgWr = rankings["win"].Get<double>() / allPicks;

				Json.Object champions = (Json.Object)rankings["cid"];
				(int id, double pbi)[] bans = new (int, double)[champions.Count];
				int i = 0;
				foreach (KeyValuePair<string, Json.Node> champion in champions) {
					int picks = champion.Value[4].Get<int>();
					//delta WR * pick rate / (1 - ban rate)
					bans[i++] = (int.Parse(champion.Key), (champion.Value[3].Get<double>() / picks - avgWr) * picks / allPicks / (1 - champion.Value[6].Get<double>() * 0.01) * 1e5);
				}
				Array.Sort(bans, (a, b) => Math.Sign(b.pbi - a.pbi));
				banSuggestions[lane] = bans;
			} catch (Exception e) {
				Console.WriteLine($"Fetching {lane} ranking data failed ({e.Message})\n{e.StackTrace}");
				banSuggestions[lane] = Array.Empty<(int, double)>();
			}
		}

		public static async Task FetchAramRanks() {
			aramRanks = new Dictionary<int, (int, double, double)>();
			try {
				Json.Node rankings = Json.Node.Parse(await http.GetStringAsync($"/tierlist/1/?patch={Client.State.currentVersion}&tier={Config.queueRankMap[Lane.ARAM]}&queue=450&region=all"));
				double avgWr = rankings["win"].Get<double>() / rankings["pick"].Get<int>();

				foreach (KeyValuePair<string, Json.Node> champion in (Json.Object)rankings["cid"]) {
					double wr = champion.Value[3].Get<double>() / champion.Value[4].Get<int>();
					aramRanks[int.Parse(champion.Key)] = (champion.Value[0].Get<int>(), wr, wr - avgWr);
				}
			} catch (Exception e) {
				Console.WriteLine($"Fetching ARAM ranking data failed ({e.Message})\n{e.StackTrace}");
			}
		}

		public static async Task<LolAlytics> FetchData(Lane lane, int championId) {
			try {
				bool isMainGameMode = lane <= Lane.Support;
				Lane queue = isMainGameMode ? Lane.Default : lane;
				Rank rank = Config.queueRankMap[queue];
				string laneString = lane.ToString().ToLower();

				//Fetch data
				string queryString = $"&p=d&v=1&cid={championId}&lane={(isMainGameMode ? laneString : "default")}&tier={rank}&queue={(isMainGameMode ? 420 : (int)queue)}&region=all";
				Json.Node data = Json.Node.Parse(await http.GetStringAsync("/mega/?ep=champion" + queryString));
				Json.Node skills = Json.Node.Parse(await http.GetStringAsync("/mega/?ep=champion2" + queryString))["skills"];
				int pickTotal = data["n"].Get<int>();

				//URL
				string url = $"https://lolalytics.com/lol/{Champion.idToChampion[championId].name}/{(isMainGameMode ? $"build/?lane={laneString}&tier={rank}" : $"{laneString}/build/?tier={rank}")}";

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
					(int category, int row, int column) = RunePage.idToTemplateIndex[int.Parse(rune.Key)];
					Json.Array stats = (Json.Array)rune.Value;

					if (row == 0) { //Keystone
						keystone[category][column] = GetVal(stats[0]);
					} else {
						primary[category, row - 1][column] = GetVal(stats[0]);
					}

					if (stats.Count > 1) {
						secondary[category, row - 1][column] = GetVal(stats[1]);
					}
				}
				int[] bestRunes = GetBestRunes(keystone, primary, secondary);

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
					bestRunes[i + 8] = bestRune;
				}

				return new LolAlytics(url, bestSkillOrder, firstSkills, bestSpells[0], bestSpells[1], new RunePage(bestRunes));
			} catch (Exception e) {
				Console.WriteLine($"Fetching LolAlytics data failed ({e.Message})\n{e.StackTrace}");
				return null;
			}
		}

		static int GetWinCount(Json.Node pickCount, Json.Node winChance) => Convert.ToInt32(pickCount.Get<int>() * winChance.Get<double>() * 0.01);
		// (pick chance) / (127/128 * pick chance + 1/128) * win chance	// Laplace smoothing, sort of
		static double GetValue(int pickTotal, int pickCount, int winCount) => (double)(128 * winCount) / (127 * pickCount + pickTotal);
		//static double GetValue(int pickTotal, int pickCount, double winChance) => (128 * pickCount * winChance) / (127 * pickCount + pickTotal);
		//static double GetValue(double pickChance, double winChance) => (128 * pickChance * winChance) / (127 * pickChance + 1);

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

		static int[] GetBestRunes(double[][] keystones, double[,][] primary, double[,][] secondary) {
			(int[] ids, double totalValue)[] bestPrimaries = new (int[], double)[RunePage.categoryCount];
			(int id1, int id2, double totalValue)[] bestSecondaries = new (int, int, double)[RunePage.categoryCount];

			for (int category = 0; category < RunePage.categoryCount; category++) {
				bestPrimaries[category].ids = new int[RunePage.rowCount];
				for (int row = 0; row < RunePage.rowCount; row++) {
					double value;
					(bestPrimaries[category].ids[row], value) = GetBestRuneInRow(RunePage.runeIds[category, row], row == 0 ? keystones[category] : primary[category, row - 1]);
					bestPrimaries[category].totalValue += value;
				}

				(int id, double value)[] secondaries = new (int, double)[RunePage.rowCount - 1];
				for (int row = 0; row < secondaries.Length; row++) {
					secondaries[row] = GetBestRuneInRow(RunePage.runeIds[category, row + 1], secondary[category, row]);
				}
				Array.Sort(secondaries, (a, b) => b.value.CompareTo(a.value)); //Reverse order
				bestSecondaries[category].id1 = secondaries[0].id;
				bestSecondaries[category].id2 = secondaries[1].id;
				bestSecondaries[category].totalValue = secondaries[0].value + secondaries[1].value;
			}

			double bestValue = 0;
			int[] bestRunes = new int[11];
			for (int primaryCategory = 0; primaryCategory < RunePage.categoryCount; primaryCategory++) {
				for (int secondaryCategory = 0; secondaryCategory < RunePage.categoryCount; secondaryCategory++) {
					if (primaryCategory == secondaryCategory) {
						continue;
					}

					double value = bestPrimaries[primaryCategory].totalValue + bestSecondaries[secondaryCategory].totalValue;
					if (value > bestValue) {
						bestValue = value;
						bestRunes[0] = RunePage.styleIds[primaryCategory];
						bestRunes[1] = RunePage.styleIds[secondaryCategory];
						Array.Copy(bestPrimaries[primaryCategory].ids, 0, bestRunes, 2, RunePage.rowCount);
						bestRunes[6] = bestSecondaries[secondaryCategory].id1;
						bestRunes[7] = bestSecondaries[secondaryCategory].id2;
					}
				}
			}
			return bestRunes;
		}

		static (int id, double value) GetBestRuneInRow(int[] ids, double[] values) {
			int bestRune = 0;
			double bestValue = 0;
			for (int column = 0; column < values.Length; column++) {
				if (values[column] > bestValue) {
					bestValue = values[column];
					bestRune = ids[column];
				}
			}
			return (bestRune, bestValue);
		}
	}
}