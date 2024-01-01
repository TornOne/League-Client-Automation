using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LCA.Client;
static class Actions {
	public static async Task<ThirdParty.Data?> LoadChampion(Champion champion, Lane lane) {
		//Runes
		ThirdParty.Data? data = await champion.GetThirdPartyData(lane);
		bool gotThirdPartyPage = data?.runePage is not null;
		bool gotPresetPage = champion.TryGetPresetPage(lane, out RunePage? runePage);
		await RunePage.Free((gotPresetPage ? 1 : 0) + (gotThirdPartyPage ? 1 : 0));

		if (lane > Lane.Support && gotPresetPage && !await runePage!.CreateRunePage($"{champion.fullName} Preset")) {
			Console.WriteLine("Preset rune page loading failed");
		}

		if (!gotThirdPartyPage) {
			Console.WriteLine("External rune page not found");
		} else if (!await data!.runePage.CreateRunePage($"{champion.fullName} {lane}")) {
			Console.WriteLine("External rune page loading failed");
		}

		if (lane <= Lane.Support && gotPresetPage && !await runePage!.CreateRunePage($"{champion.fullName} Preset")) {
			Console.WriteLine("Preset rune page loading failed");
		}

		if (lane <= Lane.Support && gotPresetPage && gotThirdPartyPage) {
			int differingRuneCount = data!.runePage.Compare(runePage!);
			if (differingRuneCount > 1) {
				Console.WriteLine($"{differingRuneCount} runes differ between preset and LolAlytics");
			}
		}

		//Spells
		if (Config.setSummonerSpells && data is not null) {
			await UpdateSummonerSpells(data.spell1Id, data.spell2Id);
		}

		//Items
		if (Config.maxItemSets > 0 && data is not null) {
			await data.itemSet.AddSet();
		}

		return data!;
	}

	static async Task UpdateSummonerSpells(int spell1Id, int spell2Id) {
		if (!(await Http.PatchJson("/lol-champ-select/v1/session/my-selection", Torn.Json.Serializer.Serialize(new Dictionary<string, object> {
			{ "spell1Id", spell1Id },
			{ "spell2Id", spell2Id }
		}))).Success) {
			Console.WriteLine("Failed to update summoner spells");
		}
	}

	public static void PrintThirdPartyData(ThirdParty.Data data, Champion champion, Lane lane) {
		Console.WriteLine($"Selected {champion.fullName} ({lane})");

		if (Config.openThirdPartySite && data.url is not null) {
			System.Diagnostics.Process.Start(data.url);
		}

		if (data is not null) {
			Console.WriteLine($"Skill order: {data.skillOrder}");
			Console.WriteLine($"First skills: {data.firstSkills}");
			Console.WriteLine();
		}
	}
}