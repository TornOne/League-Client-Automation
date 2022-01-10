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
			await FreePages((gotPresetPage ? 1 : 0) + (gotLolAlyticsPage ? 1 : 0));

			if (!gotLolAlyticsPage) {
				Console.WriteLine("LolAlytics rune page not found");
			} else if (!await CreateRunePage(lolAlytics.runePage, $"{champion.fullName} {lane}")) {
				Console.WriteLine("LolAlytics rune page loading failed");
			}

			if (gotPresetPage && !await CreateRunePage(runePage, $"{champion.fullName} Preset")) {
				Console.WriteLine("Preset rune page loading failed");
			}

			if (lane <= Lane.Support && gotPresetPage && gotLolAlyticsPage) {
				int differingRuneCount = 0;
				for (int i = 0; i < 6; i++) {
					if (!Array.Exists(lolAlytics.runePage.runes, rune => rune == runePage.runes[i])) {
						differingRuneCount++;
					}
				}
				for (int i = 6; i < 9; i++) {
					if (runePage.runes[i] != lolAlytics.runePage.runes[i]) {
						differingRuneCount++;
					}
				}

				if (differingRuneCount > 1) {
					Console.WriteLine($"{differingRuneCount} runes differ between preset and LolAlytics");
				}
			}

			//Spells
			if (Config.setSummonerSpells && lolAlytics != null) {
				await UpdateSummonerSpells(lolAlytics.spell1Id, lolAlytics.spell2Id);
			}

			return lolAlytics;
		}

		static async Task FreePages(int amount) {
			int maxPages = (await Http.GetJson("/lol-perks/v1/inventory"))["ownedPageCount"].Get<int>();
			Json.Array pages = (Json.Array)await Http.GetJson("/lol-perks/v1/pages");

			for (int i = maxPages - amount; i < pages.Count - 5; i++) {
				await Http.Delete("/lol-perks/v1/pages/" + pages[i]["id"].Get<int>());
			}
		}

		static async Task<bool> CreateRunePage(RunePage runePage, string pageName) => (await Http.PostJson("/lol-perks/v1/pages", Json.Serializer.Serialize(new Dictionary<string, object> {
			{ "autoModifiedSelections", Array.Empty<int>() },
			{ "current", true },
			{ "id", 0 },
			{ "isActive", true },
			{ "isDeletable", true },
			{ "isEditable", true },
			{ "isValid", true },
			{ "lastModified", (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds },
			{ "name", pageName },
			{ "order", 0 },
			{ "primaryStyleId", runePage.primaryStyle },
			{ "subStyleId", runePage.subStyle },
			{ "selectedPerkIds", runePage.runes }
		}))).Success;

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