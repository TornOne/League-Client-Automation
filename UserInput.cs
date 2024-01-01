using System;
using System.Threading.Tasks;

namespace LCA;
static class UserInput {
	const string availableCommands = "Available commands:\n[(save|load|delete) champion [lane] | list | exit | http (get|delete|put|post) endpoint [content]]";

	public static async Task ParseLoop() {
		while (true) {
			string[] command = Console.ReadLine()!.Split([ ' ' ], 4);
			if (command[0] == "exit") {
				Environment.Exit(0);
			} else if (command[0] == "list") {
				Champion.IteratePresetPages((champion, runePage) => Console.WriteLine($"{champion.fullName} {runePage.Key}"));
			} else if (command.Length < 2) {
				Console.WriteLine($"Too few arguments: \"{command[0]}\"");
				Console.WriteLine(availableCommands);
			} else if (command[0] == "save") {
				if (Client.State.lastRunes is null) {
					Console.WriteLine("No rune page has been modified that could be saved.");
					continue;
				}
				Console.WriteLine(Champion.SavePresetPages(command[1], Client.State.lastRunes, command.Length > 2 ? Champion.LaneFromString(command[2]) : Lane.Default) ? "Rune page successfully saved" : "No such champion found");
			} else if (command[0] == "load") {
				Champion? champion = Champion.FindByPartialName(command[1]);
				Lane lane = command.Length > 2 ? Champion.LaneFromString(command[2]) : Client.State.currentLane;
				if (champion is null) {
					Console.WriteLine("No such champion found");
					continue;
				}
				ThirdParty.Data? data = await Client.Actions.LoadChampion(champion, lane);

				if (Client.State.currentChampion is not null && data is not null) { //Check if we are in champion select
					Client.Actions.PrintThirdPartyData(data, champion, lane);
				}
			} else if (command[0] == "delete") {
				Champion? champion = Champion.FindByPartialName(command[1]);
				if (champion is null) {
					Console.WriteLine("No such champion found");
					continue;
				}
				if (!champion.TryDeletePresetPage(command.Length > 2 ? Champion.LaneFromString(command[2]) : Lane.Default)) {
					Console.WriteLine("No rune page found for that lane");
					continue;
				}
				Console.WriteLine("Delete successful");
			} else if (command[0] == "http" && command.Length > 2) {
				Console.WriteLine(command[1] == "get" ? (await Client.Http.Get(command[2])).content :
					command[1] == "delete" ? (await Client.Http.Delete(command[2])).content :
					command[1] == "put" && command.Length > 3 ? (await Client.Http.PutJson(command[2], command[3])).content :
					command[1] == "post" && command.Length > 3 ? (await Client.Http.PostJson(command[2], command[3])).content :
					$"Unknown HTTP method \"{command[1]}\"");
			} else {
				Console.WriteLine($"Unknown command: \"{string.Join(" ", command)}\"");
				Console.WriteLine(availableCommands);
			}
			Console.WriteLine();
		}
	}
}