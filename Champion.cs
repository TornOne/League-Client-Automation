using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LCA {
	//http://ddragon.leagueoflegends.com/cdn/12.11.1/data/en_US/champion.json
	class Champion {
		static readonly Dictionary<string, int> simpleNameToId = new Dictionary<string, int>();
		static readonly Dictionary<int, string> idToFullName = new Dictionary<int, string>();
		public static readonly Dictionary<int, Champion> idToChampion = new Dictionary<int, Champion>();

		public readonly int id;
		public readonly string name;
		public readonly string fullName;
		readonly Dictionary<Lane, RunePage> runePages = new Dictionary<Lane, RunePage>();
		readonly Dictionary<Lane, LolAlytics> lolAlyticsInfo = new Dictionary<Lane, LolAlytics>();

		Champion(int id, string name) {
			this.id = id;
			this.name = name;
			fullName = idToFullName[id];
		}

		static string SimplifyName(string name) {
			StringBuilder simpleName = new StringBuilder(name.Length);
			foreach (char letter in name) {
				if (letter >= 'A' && letter <= 'Z') {
					simpleName.Append((char)(letter + 32));
				} else if (letter >= 'a' && letter <= 'z' || letter >= '0' && letter <= '9') {
					simpleName.Append(letter);
				}
			}
			return simpleName.ToString();
		}

		public static Champion FindByPartialName(string partialName) {
			if (partialName == string.Empty) {
				return null;
			}

			string simplePartialName = SimplifyName(partialName);
			int idPartial = -1;
			int idStart = -1;

			foreach (KeyValuePair<string, int> nameAndId in simpleNameToId) {
				if (nameAndId.Key == simplePartialName) {
					return idToChampion[nameAndId.Value];
				}

				if (nameAndId.Key.StartsWith(simplePartialName)) {
					idStart = nameAndId.Value;
				} else if (idStart == -1 && nameAndId.Key.Contains(simplePartialName)) {
					idPartial = nameAndId.Value;
				}
			}

			return idStart == -1 && idPartial == -1 ? null : idToChampion[idStart == -1 ? idPartial : idStart];
		}

		public static Lane LaneFromString(string partialLane) {
			if (partialLane == "") {
				return Lane.Default;
			} else if (partialLane == "utility") {
				partialLane = "support";
			}

			string simplePartialLane = SimplifyName(partialLane);

			foreach (Lane lane in Enum.GetValues(typeof(Lane)) as Lane[]) {
				if (SimplifyName(lane.ToString()).StartsWith(simplePartialLane)) {
					return lane;
				}
			}

			return Lane.Default;
		}

		public static void Load() {
			foreach (string line in File.ReadLines("champions.txt")) {
				string[] champion = line.Split('\t'); //ID, Full Name
				int id = int.Parse(champion[0]);
				string simpleName = SimplifyName(champion[1]);
				simpleNameToId[simpleName] = id;
				idToFullName[id] = champion[1];
				idToChampion[id] = new Champion(id, simpleName);
			}

			if (!File.Exists("runes.txt")) {
				return;
			}

			StreamReader file = new StreamReader(File.OpenRead("runes.txt"));

			while (!file.EndOfStream) {
				string[] key = file.ReadLine().Split('\t');
				Champion champion = idToChampion[int.Parse(key[2])];
				champion.runePages[(Lane)int.Parse(key[3])] = new RunePage(file);
				file.ReadLine();
			}

			file.Close();
			Console.WriteLine("Runes loaded");
		}

		static void Save() {
			StreamWriter file = new StreamWriter(File.Create("runes.txt.temp"));

			IteratePresetPages((champion, runePage) => {
				file.WriteLine(string.Join("\t", champion.fullName, runePage.Key, champion.id, (int)runePage.Key));
				runePage.Value.WriteToFile(file);
				file.WriteLine();
			});

			file.Close();
			File.Delete("runes.txt");
			File.Move("runes.txt.temp", "runes.txt");
		}

		public static void IteratePresetPages(Action<Champion, KeyValuePair<Lane, RunePage>> Action) {
			foreach (Champion champion in idToChampion.Values) {
				foreach (KeyValuePair <Lane, RunePage> runePage in champion.runePages) {
					Action(champion, runePage);
				}
			}
		}

		public static bool SavePresetPages(string partialName, RunePage runePage, Lane lane = Lane.Default) {
			Champion champion = FindByPartialName(partialName);
			if (champion is null) {
				return false;
			}

			champion.runePages[lane] = runePage;
			Save();
			return true;
		}

		public bool TryGetPresetPage(Lane lane, out RunePage runePage) => runePages.TryGetValue(lane, out runePage) || lane != Lane.Default && runePages.TryGetValue(Lane.Default, out runePage);

		public bool TryDeletePresetPage(Lane lane) {
			if (runePages.Remove(lane)) {
				Save();
				return true;
			}
			return false;
		}

		public bool TryGetLolAlytics(Lane lane, out LolAlytics lolAlytics) => lolAlyticsInfo.TryGetValue(lane, out lolAlytics);

		public async Task<LolAlytics> GetLolAlytics(Lane lane) {
			if (!lolAlyticsInfo.TryGetValue(lane, out LolAlytics lolAlyticsLane)) {
				lolAlyticsLane = await LolAlytics.FetchData(lane, id);
				if (lolAlyticsLane != null) {
					lolAlyticsInfo[lane] = lolAlyticsLane;
				}
			}
			return lolAlyticsLane;
		}
	}
}