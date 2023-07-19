using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LCA {
	static class Config {
		public static string installPath = "C:/Program Files/Riot Games/League of Legends/";
		public static bool launchGame = false;
		public static bool openLolAlytics = true;
		public static bool setSummonerSpells = true;
		public static readonly Spell[] spellOrder = new[] {
			Spell.Placeholder,
			Spell.Mark,
			Spell.Smite,
			Spell.Ignite,
			Spell.Exhaust,
			Spell.Clarity,
			Spell.Heal,
			Spell.Barrier,
			Spell.Cleanse,
			Spell.Teleport,
			Spell.Ghost,
			Spell.Flash
		};
		public static readonly Dictionary<Lane, Rank> queueRankMap = new Dictionary<Lane, Rank> {
			{ Lane.Default, Rank.emerald_plus },
			{ Lane.ARAM, Rank.emerald_plus },
			{ Lane.URF, Rank.platinum_plus },
			{ Lane.OneForAll, Rank.platinum_plus },
			{ Lane.Nexus, Rank.platinum_plus },
			{ Lane.UltimateSpellBook, Rank.platinum_plus },
			{ Lane.ARURF, Rank.platinum_plus }
		};
		public static int banSuggestions = 3;
		public static int maxItemSets = 12;
		public static int minGamesChamp = 100;
		public static int minGamesPatch = 250000;

		public static void Load() {
			const string configPath = "config.json";

			if (File.Exists(configPath)) {
				Json.Node json = Json.Node.Parse(File.ReadAllText(configPath));
				if (!(json is Json.Object settings)) {
					return;
				}
				bool TrySetValue<T>(string name, ref T value) => settings.TryGetValue(name, out Json.Node node) && node.TryGet(out value);

				TrySetValue(nameof(installPath), ref installPath);
				if (installPath[installPath.Length - 1] != '/') {
					installPath += '/';
				}

				TrySetValue(nameof(launchGame), ref launchGame);
				TrySetValue(nameof(openLolAlytics), ref openLolAlytics);
				TrySetValue(nameof(setSummonerSpells), ref setSummonerSpells);
				TrySetValue(nameof(banSuggestions), ref banSuggestions);
				TrySetValue(nameof(maxItemSets), ref maxItemSets);
				TrySetValue(nameof(minGamesChamp), ref minGamesChamp);
				TrySetValue(nameof(minGamesPatch), ref minGamesPatch);

				if (settings.TryGetValue(nameof(spellOrder), out Json.Node spellsNode) && spellsNode is Json.Array spells) {
					for (int i = 0; i < spells.Count; i++) {
						if (spells[i].TryGet(out string spellString) && Enum.TryParse(spellString, out Spell spell) && spellOrder[i] != spell) {
							int oldIndex = Array.IndexOf(spellOrder, spell);
							spellOrder[oldIndex] = spellOrder[i];
							spellOrder[i] = spell;
						}
					}
				}

				if (settings.TryGetValue(nameof(queueRankMap), out Json.Node ranksNode) && ranksNode is Json.Object ranks) {
					Queue<Action> updates = new Queue<Action>();
					foreach (Lane queue in queueRankMap.Keys) {
						if (ranks.TryGetValue(queue.ToString(), out Json.Node rankNode) && rankNode.TryGet(out string rankString) && Enum.TryParse(rankString, out Rank rank)) {
							updates.Enqueue(() => queueRankMap[queue] = rank);
						}
					}
					while (updates.Count > 0) {
						updates.Dequeue()();
					}
				}

				Console.WriteLine("Settings loaded");
			} else {
				Console.WriteLine("Settings not found, using defaults");
			}

			//Save
			File.WriteAllText(configPath, Json.Serializer.Serialize(new Dictionary<string, object> {
				{ nameof(installPath), installPath },
				{ nameof(launchGame), launchGame },
				{ nameof(openLolAlytics), openLolAlytics },
				{ nameof(setSummonerSpells), setSummonerSpells },
				{ nameof(spellOrder), Array.ConvertAll(spellOrder, spell => spell.ToString()) },
				{ nameof(queueRankMap), queueRankMap.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString()) },
				{ nameof(banSuggestions), banSuggestions },
				{ nameof(maxItemSets), maxItemSets },
				{ nameof(minGamesChamp), minGamesChamp },
				{ nameof(minGamesPatch), minGamesPatch }
			}, true));
		}
	}
}