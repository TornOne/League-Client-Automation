using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LCA.Client {
	static class Actions {
		public static async Task LoadRunePages(Champion champion, Lane lane, RunePage lolAlyticsRunePage) {
			await FreePages(2);

			if (lolAlyticsRunePage is null) {
				Console.WriteLine("LolAlytics rune page not found");
			} else if (await CreateRunePage(lolAlyticsRunePage, $"{champion.fullName} {lane}")) {
				Console.WriteLine("LolAlytics rune page loaded");
			} else {
				Console.WriteLine("LolAlytics rune page loading failed");
			}

			if (!champion.TryGetRunePage(out RunePage runePage, lane)) {
				Console.WriteLine("Preset rune page not found");
			} else if (await CreateRunePage(runePage, champion.fullName)) {
				Console.WriteLine("Preset rune page loaded");
			} else {
				Console.WriteLine("Preset rune page loading failed");
			}

			if (runePage != null && lolAlyticsRunePage != null) {
				int differingRuneCount = 0;
				for (int i = 0; i < 6; i++) {
					if (!Array.Exists(lolAlyticsRunePage.runes, rune => rune == runePage.runes[i])) {
						differingRuneCount++;
					}
				}
				for (int i = 6; i < 9; i++) {
					if (runePage.runes[i] != lolAlyticsRunePage.runes[i]) {
						differingRuneCount++;
					}
				}

				if (differingRuneCount > 1) {
					Console.WriteLine($"{differingRuneCount} runes differ between preset and LolAlytics");
				}
			}
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
			if ((await Http.PatchJson("/lol-champ-select/v1/session/my-selection", Json.Serializer.Serialize(new Dictionary<string, object> {
				{ "spell1Id", spell1Id },
				{ "spell2Id", spell2Id }
			}))).Success) {
				Console.WriteLine("Summoner spells successfully updated");
			} else {
				Console.WriteLine("Failed to update summoner spells");
			}
		}

		public static async Task PrintLolAlyticsData(LolAlytics lolAlyticsData) {
			if (lolAlyticsData is null) {
				return;
			}
			if (Config.setSummonerSpells) {
				await UpdateSummonerSpells(lolAlyticsData.spell1Id, lolAlyticsData.spell2Id);
			}
			Console.WriteLine($"Skill order: {lolAlyticsData.skillOrder}");
			Console.WriteLine($"First skills: {lolAlyticsData.firstSkills}");
			Console.WriteLine();
		}
	}
}