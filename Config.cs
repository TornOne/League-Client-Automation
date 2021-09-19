using System;
using System.Collections.Generic;
using System.IO;
using Torn.Json;

static class Config {
	public static string installPath = "C:\\Program Files\\Riot Games\\League of Legends\\";
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

	public static void Load() {
		const string configPath = "config.json";

		if (File.Exists(configPath)) {
			Dictionary<string, object> settings = Json.Deserialize(File.ReadAllText(configPath)) as Dictionary<string, object>;
			void TrySetValue<T>(string name, ref T value) {
				if (settings.TryGetValue(name, out object obj) && obj is T val) {
					value = val;
				}
			}

			TrySetValue(nameof(installPath), ref installPath);
			if (installPath[installPath.Length - 1] != '\\') {
				installPath += '\\';
			}

			TrySetValue(nameof(openLolAlytics), ref openLolAlytics);
			TrySetValue(nameof(setSummonerSpells), ref setSummonerSpells);

			if (settings.TryGetValue(nameof(spellOrder), out object spellOrderObj) && spellOrderObj is List<object> spellOrderList) {
				for (int i = 0; i < spellOrderList.Count; i++) {
					if (Enum.TryParse(spellOrderList[i] as string, out Spell spell) && spellOrder[i] != spell) {
						int oldIndex = Array.IndexOf(spellOrder, spell);
						spellOrder[oldIndex] = spellOrder[i];
						spellOrder[i] = spell;
					}
				}
			}

			Console.WriteLine("Settings loaded");
		} else {
			Console.WriteLine("Settings not found, using defaults");
		}

		//Save
		File.WriteAllText(configPath, Json.PrettyPrint(Json.Serialize(new Dictionary<string, object> {
			{ nameof(installPath), installPath },
			{ nameof(openLolAlytics), openLolAlytics },
			{ nameof(setSummonerSpells), setSummonerSpells },
			{ nameof(spellOrder), Array.ConvertAll(spellOrder, spell => spell.ToString()) }
		})));
	}
}
