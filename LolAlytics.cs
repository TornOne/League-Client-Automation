using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Torn.Json;

enum Spell {
	Cleanse = 1,
	Exhaust = 3,
	Flash = 4,
	Ghost = 6,
	Heal = 7,
	Smite = 11,
	Teleport = 12,
	Clarity = 13,
	Ignite = 14,
	Barrier = 21,
	Mark = 32,
	Placeholder = 54,
}

class LolAlytics {
	static readonly HttpClient http = new HttpClient {
		BaseAddress = new Uri("https://apix1.op.lol")
	};
	static readonly Dictionary<Lane, int> laneToQueueMap = new Dictionary<Lane, int> {
		{ Lane.Default, 420 },
		{ Lane.Top, 420 },
		{ Lane.Jungle, 420 },
		{ Lane.Middle, 420 },
		{ Lane.Bottom, 420 },
		{ Lane.Support, 420 },
		{ Lane.ARAM, 450 },
		{ Lane.URF, 900 },
		{ Lane.OneForAll, 1020 },
		{ Lane.Nexus, 1300 },
		{ Lane.UltimateSpellBook, 1400 }
	};
	public static readonly Dictionary<int, Lane> queueToLaneMap = new Dictionary<int, Lane> {
		{ 420, Lane.Default },
		{ 450, Lane.ARAM },
		{ 900, Lane.URF },
		{ 1020, Lane.OneForAll },
		{ 1300, Lane.Nexus },
		{ 1400, Lane.UltimateSpellBook }
	};
	public static readonly Dictionary<Spell, int> spellToOrderMap = new Dictionary<Spell, int>();

	public readonly string skillOrder, firstSkills;
	public readonly int spell1Id, spell2Id;
	public readonly RunePage runePage;

	LolAlytics(string skillOrder, string firstSkills, int spell1Id, int spell2Id, RunePage runePage) {
		this.skillOrder = skillOrder;
		this.firstSkills = firstSkills;
		this.spell1Id = spell1Id;
		this.spell2Id = spell2Id;
		this.runePage = runePage;
	}

	public static async Task<LolAlytics> FetchDataAsync(Lane lane, int championId) {
		try {
			string queryString = $"&p=d&v=8&cid={championId}&lane={(lane >= Lane.Top && lane <= Lane.Support ? lane.ToString().ToLower() : "default")}&tier={(lane <= Lane.ARAM ? "platinum_plus" : "all")}&queue={laneToQueueMap[lane]}&region=all";
			Dictionary<string, object> data = Json.Deserialize(await http.GetStringAsync("/mega/?ep=champion" + queryString)) as Dictionary<string, object>;
			Dictionary<string, object> skills = (Json.Deserialize(await http.GetStringAsync("/mega/?ep=champion2" + queryString)) as Dictionary<string, object>)["skills"] as Dictionary<string, object>;
			int pickTotal = (int)data["n"];

			//Skill order
			string bestSkillOrder = "";
			double bestValue = 0;
			foreach (List<object> skillOrder in skills["skillOrder"] as List<object>) {
				double value = GetValue(pickTotal, (int)skillOrder[1], (int)skillOrder[2]);
				if (value > bestValue) {
					bestValue = value;
					bestSkillOrder = skillOrder[0] as string;
				}
			}
			bestSkillOrder = string.Join(" > ", bestSkillOrder.ToCharArray());

			//First skills (PS. "skillOrder" exists too)
			string firstSkills = "";
			Dictionary<string, (int picks, int wins)> allSkills = new Dictionary<string, (int picks, int wins)>();
			foreach (List<object> sixSkills in skills["skill6"] as List<object>) {
				string fiveSkills = sixSkills[0].ToString().Substring(0, 5);
				if (allSkills.TryGetValue(fiveSkills, out (int picks, int wins) stats)) {
					allSkills[fiveSkills] = (stats.picks + (int)sixSkills[1], stats.wins + (int)sixSkills[2]);
				} else {
					allSkills[fiveSkills] = ((int)sixSkills[1], (int)sixSkills[2]);
				}
			}
			for (int i = 0; i < 5; i++) {
				firstSkills += ChooseNextSkill(pickTotal, allSkills, firstSkills);
			}
			firstSkills = string.Join(" -> ", Array.ConvertAll(firstSkills.ToCharArray(), c => c == '1' ? 'Q' : c == '2' ? 'W' : c == '3' ? 'E' : 'R'));

			//Spells
			string spells = "";
			bestValue = 0;
			foreach (List<object> spellsData in data["spells"] as List<object>) {
				double value = GetValue(pickTotal, (int)spellsData[3], GetWinCount(spellsData[3], spellsData[1]));
				if (value > bestValue) {
					bestValue = value;
					spells = spellsData[0] as string;
				}
			}
			int[] bestSpells = Array.ConvertAll(spells.Split('_'), int.Parse);
			Array.Sort(bestSpells, (x, y) => spellToOrderMap[(Spell)x] - spellToOrderMap[(Spell)y]);

			//Runes
			Dictionary<string, object> runes = (data["runes"] as Dictionary<string, object>)["stats"] as Dictionary<string, object>;
			string[] smallRunes = new[] { "5008", "5005", "5007", "5008f", "5002f", "5003f", "5001", "5002", "5003" };

			(int[][,] keystone, int[,][,] primary) = RunePage.PrimaryTemplate;
			int[,][,] secondary = RunePage.SecondaryTemplate;
			foreach (KeyValuePair<string, object> rune in runes) {
				if (Array.Exists(smallRunes, id => id == rune.Key)) {
					continue;
				}
				(int category, int row, int column) = RunePage.idToTemplateIndex[int.Parse(rune.Key)];
				List<object> stats = rune.Value as List<object>;

				List<object> counts = stats[0] as List<object>;
				if (row == -1) { //Keystone
					keystone[category][column, 0] = (int)counts[2];
					keystone[category][column, 1] = GetWinCount(counts[2], counts[1]);
				} else {
					primary[category, row][column, 0] = (int)counts[2];
					primary[category, row][column, 1] = GetWinCount(counts[2], counts[1]);
				}

				if (stats.Count > 1) {
					counts = stats[1] as List<object>;
					secondary[category, row][column, 0] = (int)counts[2];
					secondary[category, row][column, 1] = GetWinCount(counts[2], counts[1]);
				}
			}
			int[] bestRunes = GetBestRunes(pickTotal, keystone, primary, secondary);

			for (int i = 0; i < 3; i++) {
				int bestRune = 0;
				bestValue = 0;
				for (int j = 0; j < 3; j++) {
					string runeId = smallRunes[i * 3 + j];
					List<object> counts = (runes[runeId] as List<object>)[0] as List<object>;
					double value = GetValue(pickTotal, (int)counts[2], GetWinCount(counts[2], counts[1]));
					if (value > bestValue) {
						bestValue = value;
						bestRune = int.Parse(runeId.Substring(0, 4));
					}
				}
				bestRunes[i + 8] = bestRune;
			}

			Console.WriteLine("LolAlytics data successfully fetched");
			return new LolAlytics(bestSkillOrder, firstSkills, bestSpells[0], bestSpells[1], new RunePage(bestRunes));
		} catch (Exception e) {
			Console.WriteLine($"Fetching LolAlytics data failed ({e.Message})\n{e.StackTrace}");
			return null;
		}
	}

	static int GetWinCount(object pickCount, object winChance) => Convert.ToInt32((int)pickCount * (winChance is double percentage ? percentage : (int)winChance) * 0.01);
	// (pick chance) / (127/128 * pick chance + 1/128) * win chance   // Laplace smoothing, sort of
	static double GetValue(int pickTotal, int pickCount, int winCount) => (double)(128 * winCount) / (127 * pickCount + pickTotal);
	//static double GetValue(int pickTotal, int pickCount, double winChance) => (128 * pickCount * winChance) / (127 * pickCount + pickTotal);
	//static double GetValue(double pickChance, double winChance) => (128 * pickChance * winChance) / (127 * pickChance + 1);

	static char ChooseNextSkill(int pickTotal, Dictionary<string, (int picks, int wins)> allSkills, string given) {
		Dictionary<char, (int picks, int wins)> nextSkills = new Dictionary<char, (int picks, int wins)> {
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

	static int[] GetBestRunes(int pickTotal, int[][,] keystones, int[,][,] primary, int[,][,] secondary) {
		(int, int, int, int, double totalValue)[] bestPrimaries = new (int, int, int, int, double)[5];
		(int, int, double totalValue)[] bestSecondaries = new (int, int, double)[5];

		for (int category = 0; category < 5; category++) {
			double value;
			(bestPrimaries[category].Item1, bestPrimaries[category].totalValue) = GetBestRuneInRow(pickTotal, category, -1, keystones[category]);
			(bestPrimaries[category].Item2, value) = GetBestRuneInRow(pickTotal, category, 0, primary[category, 0]);
			bestPrimaries[category].totalValue += value;
			(bestPrimaries[category].Item3, value) = GetBestRuneInRow(pickTotal, category, 1, primary[category, 1]);
			bestPrimaries[category].totalValue += value;
			(bestPrimaries[category].Item4, value) = GetBestRuneInRow(pickTotal, category, 2, primary[category, 2]);
			bestPrimaries[category].totalValue += value;

			double value1, value2;
			(bestSecondaries[category].Item1, value1) = GetBestRuneInRow(pickTotal, category, 0, secondary[category, 0]);
			(bestSecondaries[category].Item2, value2) = GetBestRuneInRow(pickTotal, category, 1, secondary[category, 1]);
			(int item3, double value3) = GetBestRuneInRow(pickTotal, category, 2, secondary[category, 2]);
			if (value3 > value1 && value2 > value1) {
				bestSecondaries[category].Item1 = item3;
				bestSecondaries[category].totalValue = value2 + value3;
			} else if (value3 > value2 && value1 > value2) {
				bestSecondaries[category].Item2 = item3;
				bestSecondaries[category].totalValue = value1 + value3;
			} else {
				bestSecondaries[category].totalValue = value1 + value2;
			}
		}

		double bestValue = 0;
		int[] bestRunes = new int[11];
		for (int primaryCategory = 0; primaryCategory < 5; primaryCategory++) {
			for (int secondaryCategory = 0; secondaryCategory < 5; secondaryCategory++) {
				if (primaryCategory == secondaryCategory) {
					continue;
				}

				double value = bestPrimaries[primaryCategory].totalValue + bestSecondaries[secondaryCategory].totalValue;
				if (value > bestValue) {
					bestValue = value;
					bestRunes[0] = RunePage.styleIds[primaryCategory];
					bestRunes[1] = RunePage.styleIds[secondaryCategory];
					bestRunes[2] = bestPrimaries[primaryCategory].Item1;
					bestRunes[3] = bestPrimaries[primaryCategory].Item2;
					bestRunes[4] = bestPrimaries[primaryCategory].Item3;
					bestRunes[5] = bestPrimaries[primaryCategory].Item4;
					bestRunes[6] = bestSecondaries[secondaryCategory].Item1;
					bestRunes[7] = bestSecondaries[secondaryCategory].Item2;
				}
			}
		}
		return bestRunes;
	}

	static (int id, double value) GetBestRuneInRow(int pickTotal, int category, int row, int[,] runes) {
		int bestRune = 0;
		double bestValue = 0;
		for (int column = 0; column < runes.GetLength(0); column++) {
			double value = GetValue(pickTotal, runes[column, 0], runes[column, 1]);
			if (value > bestValue) {
				bestValue = value;
				bestRune = RunePage.templateIndexToId[(category, row, column)];
			}
		}
		return (bestRune, bestValue);
	}
}
