using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Torn.Json;

namespace LCA;
static class Config {
	public static string installPath = "C:/Program Files/Riot Games/League of Legends/";
	public static bool launchGame = false;
	public static bool setSummonerSpells = true;
	public static readonly Spell[] spellOrder = [
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
	];
	public static int banSuggestions = 3;
	public static int eventBanSuggestions = 6;
	public static int maxItemSets = 12;
	public static int minGamesChamp = 1000;
	public static ThirdParty.API thirdPartyInterface = ThirdParty.API.LolAlytics;
	public static bool openThirdPartySite = false;
	public static readonly Dictionary<Lane, ThirdParty.LolAlytics.Rank> lolAlyticsQueueRankMap = new() {
		{ Lane.Default, ThirdParty.LolAlytics.Rank.emerald_plus },
		{ Lane.ARAM, ThirdParty.LolAlytics.Rank.platinum_plus },
		{ Lane.URF, ThirdParty.LolAlytics.Rank.platinum_plus },
		{ Lane.OneForAll, ThirdParty.LolAlytics.Rank.platinum_plus },
		{ Lane.Nexus, ThirdParty.LolAlytics.Rank.platinum_plus },
		{ Lane.UltimateSpellBook, ThirdParty.LolAlytics.Rank.platinum_plus },
		{ Lane.ARURF, ThirdParty.LolAlytics.Rank.platinum_plus },
		{ Lane.Arena, ThirdParty.LolAlytics.Rank.platinum_plus }
	};

	public static void Load() {
		const string configPath = "config.json";

		if (!File.Exists(configPath)) {
			Console.WriteLine("Settings not found, using defaults");
			goto Save;
		}

		JsonDocument json;
		try {
			json = JsonDocument.Parse(File.ReadAllBytes(configPath));
		} catch {
			Console.WriteLine("Failed to parse settings");
			goto Save;
		}

		JsonElement settings = json.RootElement;
		if (settings.ValueKind != JsonValueKind.Object) {
			goto Save;
		}

		TrySetValue(settings, nameof(installPath), ref installPath);
		installPath = installPath.Replace('\\', '/');
		if (installPath[^1] != '/') {
			installPath += '/';
		}

		TrySetValue(settings, nameof(launchGame), ref launchGame);
		TrySetValue(settings, nameof(setSummonerSpells), ref setSummonerSpells);
		TrySetValue(settings, nameof(banSuggestions), ref banSuggestions);
		TrySetValue(settings, nameof(eventBanSuggestions), ref eventBanSuggestions);
		TrySetValue(settings, nameof(maxItemSets), ref maxItemSets);
		TrySetValue(settings, nameof(minGamesChamp), ref minGamesChamp);
		if (settings.TryGetProperty(nameof(thirdPartyInterface), out JsonElement node) && node.TryGetValue(out int value)) {
			thirdPartyInterface = (ThirdParty.API)value;
		}
		TrySetValue(settings, nameof(openThirdPartySite), ref openThirdPartySite);

		if (settings.TryGetValue(nameof(spellOrder), out JsonElement spells) && spells.ValueKind == JsonValueKind.Array) {
			int spellCount = spells.GetArrayLength();
			for (int i = 0; i < spellCount; i++) {
				if (spells[i].TryGetValue(out string spellString) && Enum.TryParse(spellString, out Spell spell) && spellOrder[i] != spell) {
					int oldIndex = Array.IndexOf(spellOrder, spell);
					spellOrder[oldIndex] = spellOrder[i];
					spellOrder[i] = spell;
				}
			}
		}

		if (settings.TryGetValue(nameof(lolAlyticsQueueRankMap), out JsonElement ranks) && ranks.ValueKind == JsonValueKind.Object) {
			Queue<Action> updates = [];
			foreach (Lane queue in lolAlyticsQueueRankMap.Keys) {
				if (ranks.TryGetValue(queue.ToString(), out JsonElement rankNode) && rankNode.TryGetValue(out string rankString) && Enum.TryParse(rankString, out ThirdParty.LolAlytics.Rank rank)) {
					updates.Enqueue(() => lolAlyticsQueueRankMap[queue] = rank);
				}
			}
			while (updates.Count > 0) {
				updates.Dequeue()();
			}
		}

		json.Dispose();
		Console.WriteLine("Settings loaded");

	Save:
		using FileStream config = File.Open(configPath, FileMode.Create, FileAccess.Write);
		Serializer.Serialize(config, new Dictionary<string, object> {
			{ nameof(installPath), installPath },
			{ nameof(launchGame), launchGame },
			{ nameof(setSummonerSpells), setSummonerSpells },
			{ nameof(spellOrder), Array.ConvertAll(spellOrder, spell => spell.ToString()) },
			{ nameof(banSuggestions), banSuggestions },
			{ nameof(eventBanSuggestions), eventBanSuggestions },
			{ nameof(maxItemSets), maxItemSets },
			{ nameof(minGamesChamp), minGamesChamp },
			{ nameof(thirdPartyInterface), (int)thirdPartyInterface },
			{ nameof(openThirdPartySite), openThirdPartySite },
			{ nameof(lolAlyticsQueueRankMap), lolAlyticsQueueRankMap.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString()) }
		}, true);
	}

	static bool TrySetValue(JsonElement el, string name, ref bool value) => el.TryGetProperty(name, out JsonElement node) && node.TryGetValue(out value);
	static bool TrySetValue(JsonElement el, string name, ref int value) => el.TryGetProperty(name, out JsonElement node) && node.TryGetValue(out value);
	static bool TrySetValue(JsonElement el, string name, ref string value) => el.TryGetProperty(name, out JsonElement node) && node.TryGetValue(out value);
}
