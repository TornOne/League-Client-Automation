using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace LCA;
class ItemSet(int champion, Lane lane, ItemSet.Block[] blocks) {
	public class Block(string name, int[] items) {
		public readonly string name = name;
		public readonly int[] items = items;
	}

	readonly int champion = champion;
	readonly Lane lane = lane;
	readonly Block[] blocks = blocks;

	public async Task AddSet() {
		string championName = Champion.idToChampion[champion].fullName;

		//Get all sets
		JsonElement setsJson = (await Client.Http.Get($"/lol-item-sets/v1/item-sets/{Client.State.summonerId}/sets")).AsJson()!.RootElement;
		JsonElement allSets = setsJson.GetProperty("itemSets");
		List<string> foreignSets = [];
		List<string> ourSets = [];
		string newSet = Torn.Json.Serializer.Serialize(new Dictionary<string, object> {
			{ "associatedChampions", new[] { champion } },
			{ "associatedMaps", Array.Empty<int>() },
			{ "blocks", Array.ConvertAll(blocks, block => new Dictionary<string, object> {
				{ "hideIfSummonerSpell", string.Empty },
				{ "items", Array.ConvertAll(block.items, item => new Dictionary<string, object> {
					{ "count", 1 },
					{ "id", item.ToString() }
				}) },
				{ "showIfSummonerSpell", string.Empty },
				{ "type", block.name }
			}) },
			{ "map", "any" },
			{ "mode", "any" },
			{ "preferredItemSlots", Array.Empty<int>() },
			{ "sortrank", 100 },
			{ "startedFrom", "blank" },
			{ "title", $"{championName} {lane} (LCA)" },
			{ "type", "custom" }
		});

		//Make sure all sets not made by us are untouched, and don't add any of our sets for this champion
		foreach (JsonElement itemSet in allSets.EnumerateArray()) {
			string title = itemSet.GetProperty("title").GetString()!;
			if (!title.EndsWith("(LCA)")) {
				foreignSets.Add(itemSet.ToString());
			} else if (!title.StartsWith(championName)) {
				ourSets.Add(itemSet.ToString());
			}
		}

		//Ensure capacity by removing a random item set from our sets if at capacity
		if (ourSets.Count >= Config.maxItemSets) {
			ourSets.RemoveAt(Random.Shared.Next(ourSets.Count));
		}
		ourSets.AddRange(foreignSets);
		ourSets.Add(newSet);

		Client.Http.Response response = await Client.Http.PutJson($"/lol-item-sets/v1/item-sets/{Client.State.summonerId}/sets", $"{{\"accountId\":{setsJson.GetProperty("accountId").GetInt64()},\"itemSets\":[{string.Join(",", ourSets)}],\"timestamp\":{setsJson.GetProperty("timestamp").GetInt64()}}}");
		if (!response.Success) {
			//TODO: Starts failing at around 30 item sets due to the payload being too large.
			//Might be able to fix by moving to WebSockets or if there is a way to send only partial item set updates
			Console.WriteLine("Failed to update item sets");
		}
	}
}
