using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LCA.Client {
	static class Actions {
		public static async Task<LolAlytics> LoadChampion(Champion champion, Lane lane) {
			//Runes
			LolAlytics lolAlytics = await champion.GetLolAlytics(lane);
			bool gotLolAlyticsPage = lolAlytics?.runePage != null;
			bool gotPresetPage = champion.TryGetPresetPage(lane, out RunePage runePage);
			await RunePage.Free((gotPresetPage ? 1 : 0) + (gotLolAlyticsPage ? 1 : 0));

			if (!gotLolAlyticsPage) {
				Console.WriteLine("LolAlytics rune page not found");
			} else if (!await lolAlytics.runePage.CreateRunePage($"{champion.fullName} {lane}")) {
				Console.WriteLine("LolAlytics rune page loading failed");
			}

			if (gotPresetPage && !await runePage.CreateRunePage($"{champion.fullName} Preset")) {
				Console.WriteLine("Preset rune page loading failed");
			}

			if (lane <= Lane.Support && gotPresetPage && gotLolAlyticsPage) {
				int differingRuneCount = lolAlytics.runePage.Compare(runePage);
				if (differingRuneCount > 1) {
					Console.WriteLine($"{differingRuneCount} runes differ between preset and LolAlytics");
				}
			}

			//Spells
			if (Config.setSummonerSpells && lolAlytics != null) {
				await UpdateSummonerSpells(lolAlytics.spell1Id, lolAlytics.spell2Id);
			}

			//Items
			if (Config.maxItemSets > 0 && lolAlytics != null) {
				await lolAlytics.itemSet.AddSet();
			}

			return lolAlytics;
		}

		static async Task UpdateSummonerSpells(int spell1Id, int spell2Id) {
			if (!(await Http.PatchJson("/lol-champ-select/v1/session/my-selection", Json.Serializer.Serialize(new Dictionary<string, object> {
				{ "spell1Id", spell1Id },
				{ "spell2Id", spell2Id }
			}))).Success) {
				Console.WriteLine("Failed to update summoner spells");
			}
		}

		public static void PrintLolAlytics(LolAlytics lolAlytics, Champion champion, Lane lane) {
			Console.WriteLine($"Selected {champion.fullName} ({lane})");

			if (Config.openLolAlytics) {
				System.Diagnostics.Process.Start(lolAlytics.url);
			}

			if (lolAlytics != null) {
				Console.WriteLine($"Skill order: {lolAlytics.skillOrder}");
				Console.WriteLine($"First skills: {lolAlytics.firstSkills}");
				Console.WriteLine();
			}
		}
	}
}