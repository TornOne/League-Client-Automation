using System;
using System.Threading.Tasks;

namespace LCA {
	//TODO: Figure out where you want empty lines as pretty formatting (currently double empty lines sometimes, like upon "load")
	static class UserInput {
		public static async Task ParseLoop() {
			while (true) {
				string[] command = Console.ReadLine().Split(new[] { ' ' }, 4);
				if (command[0] == "help") {
					Console.WriteLine("Available commands:");
					Console.WriteLine("save champion [lane] - Saves the last modified rune page to the specified champion. If a lane is not specified, the page will be saved as default and be used whenever a specific lane's page is not available.");
					Console.WriteLine("load champion [lane] - Loads both the saved and the LolAlytics rune pages of the specified champion. If a lane is not specified, the default lane will be used for the saved page, and the most popular lane for the champion will be used for the LolAlytics page.");
					Console.WriteLine("delete champion [lane] - Deletes the rune page associated with the specified champion and lane. If a lane is not specified, the default page will be deleted.");
					Console.WriteLine("Champion and lane names are not case sensitive and may be partial, but have to be without spaces.");
					Console.WriteLine("Event gamemodes may be used as lane names. These include ARAM, URF, OneForAll, Nexus, UltimateSpellBook.");
				} else if (command[0] == "exit") {
					Environment.Exit(0);
				} else if (command.Length < 2) {
					Console.WriteLine($"Too few arguments: \"{command[0]}\"");
					Console.WriteLine("Enter \"help\" for a list of commands");
				} else if (command[0] == "save") {
					Console.WriteLine(Champion.SaveRunePage(command[1], Client.State.lastRunes, command.Length > 2 ? Champion.LaneFromString(command[2]) : Lane.Default) ? "Rune page successfully saved" : "No such champion found");
				} else if (command[0] == "load") {
					Champion champion = Champion.FindByPartialName(command[1]);
					if (champion is null) {
						Console.WriteLine("No such champion found");
						continue;
					}
					Lane lane = command.Length > 2 ? Champion.LaneFromString(command[2]) : Lane.Default;
					LolAlytics lolAlyticsData = await champion.GetLolAlytics(lane);
					await Client.Actions.LoadRunePages(champion, lane, lolAlyticsData?.runePage);
					await Client.Actions.PrintLolAlyticsData(lolAlyticsData);
				} else if (command[0] == "delete") {
					Champion champion = Champion.FindByPartialName(command[1]);
					if (champion is null) {
						Console.WriteLine("No such champion found");
						continue;
					}
					if (!champion.TryDeleteRunePage(command.Length > 2 ? Champion.LaneFromString(command[2]) : Lane.Default)) {
						Console.WriteLine("No rune page found for that lane");
						continue;
					}
					Console.WriteLine("Delete successful");
				} else if (command[0] == "http" && command.Length > 2) {
					Console.WriteLine(command[1] == "get" ? (await Client.Http.GetJson(command[2])).ToString(true) :
						command[1] == "delete" ? (await Client.Http.Delete(command[2])).content :
						command[1] == "put" && command.Length > 3 ? (await Client.Http.PutJson(command[2], command[3])).content :
						command[1] == "post" && command.Length > 3 ? (await Client.Http.PostJson(command[2], command[3])).content :
						$"Unknown HTTP method \"{command[1]}\"");
				} else {
					Console.WriteLine($"Unknown command: \"{string.Join(" ", command)}\"");
					Console.WriteLine("Enter \"help\" for a list of commands");
				}
				Console.WriteLine();
			}
		}
	}
}