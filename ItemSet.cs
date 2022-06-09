using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LCA {
	class ItemSet {
		class Block {
			public readonly string name;
			public readonly int[] items;

			public Block(string name, int[] items) {
				this.name = name;
				this.items = items;
			}
		}

		readonly int champion;
		readonly Lane lane;
		readonly List<Block> blocks = new List<Block>(10);

		public ItemSet(int champion, Lane lane, Json.Node data, Json.Node data2) {
			this.champion = champion;
			this.lane = lane;

			blocks.Add(SelectBest("Start", (Json.Array)data2["startSet"], 4));
			for (int i = 1; i <= 5; i++) {
				if ((data as Json.Object).TryGetValue($"item{i}", out Json.Node items)) {
					blocks.Add(SelectBest($"Item {i}", (Json.Array)items, 6));
				}
			}
			blocks.Add(SelectBest("Overall", (Json.Array)data["popularItem"], 12));

			if (champion == 69 || champion == 350) { //Cassiopeia and Yuumi don't use boots
				return;
			}

			HashSet<string> possibleBoots = new HashSet<string>();
			foreach (Json.Array boot in (Json.Array)data["boots"]) {
				possibleBoots.Add(boot[0].Get<int>().ToString());
			}
			possibleBoots.Remove("9999"); //No boots
			int bootsPickedFirst = 0;
			int bootsWonFirst = 0;
			int bootsPickedSecond = 0;
			int bootsWonSecond = 0;
			for (int i = 2; i <= 6; i++) {
				if (!(data2["itemSets"] as Json.Object).TryGetValue($"itemBootSet{i}", out Json.Node itemBootSet)) {
					continue;
				}
				foreach (KeyValuePair<string, Json.Node> pair in (Json.Object)itemBootSet) {
					string[] items = pair.Key.Split('_');
					int picks = pair.Value[0].Get<int>();
					int wins = pair.Value[1].Get<int>();
					if (possibleBoots.Contains(items[0])) {
						bootsPickedFirst += picks;
						bootsWonFirst += wins;
					} else if (possibleBoots.Contains(items[1])) {
						bootsPickedSecond += picks;
						bootsWonSecond += wins;
					}
				}
			}
			double bootsDiff = (double)(128 * bootsWonFirst) / (128 * bootsPickedFirst + bootsPickedSecond) - (double)(128 * bootsWonSecond) / (128 * bootsPickedSecond + bootsPickedFirst);

			blocks.Insert(bootsDiff > 0 ? 1 : 2, SelectBest($"Boots +{Math.Abs(bootsDiff):0%} {(bootsDiff > 0 ? "1st" : "2nd")}", (Json.Array)data["boots"], 6));
		}

		//Select all items that have at least some % of the value of the best item
		static Block SelectBest(string nameBase, Json.Array itemsJson, int maxItems) {
			double GetValue(Json.Array item) {
				double pickChance = item[2].Get<double>() * 0.01;
				return 1.28 * pickChance * item[1].Get<double>() / (127 * pickChance + 1);
			}

			List<KeyValuePair<int[], double>> items = new List<KeyValuePair<int[], double>>(itemsJson.Count);
			foreach (Json.Array item in itemsJson) {
				int[] ids;
				if (item[0].TryGet(out int id)) {
					if (id == 3041) { //Do not suggest Mejai - it is far too large an outlier
						continue;
					}
					ids = new[] { id };
				} else {
					string idString = item[0].Get<string>();
					ids = idString == string.Empty ? Array.Empty<int>() : Array.ConvertAll(idString.Split('_'), int.Parse);
				}
				items.Add(new KeyValuePair<int[], double>(ids, GetValue(item)));
			}
			items.Sort((a, b) => Math.Sign(b.Value - a.Value));

			StringBuilder name = new StringBuilder(nameBase);
			name.Append(" (");
			List<int> bestItems = new List<int>(maxItems);
			double highestValue = items[0].Value;
			int count = 0;
			foreach (KeyValuePair<int[], double> item in items) {
				if (item.Value < highestValue * (Math.Sqrt(48d / maxItems * count + 1) + 1) / 8) {
					break;
				}
				bestItems.AddRange(item.Key);
				name.Append($"{item.Value * 100 - 50:0.0}, ");
				count++;
			}
			name.Remove(name.Length - 2, 2);
			name.Append(')');

			return new Block(name.ToString(), bestItems.ToArray());
		}

		public async Task AddSet() {
			string championName = Champion.idToChampion[champion].fullName;

			//Get all sets
			Json.Node setsJson = await Client.Http.GetJson($"/lol-item-sets/v1/item-sets/{Client.State.summonerId}/sets");
			Json.Array allSets = (Json.Array)setsJson["itemSets"];
			List<string> foreignSets = new List<string>();
			List<string> ourSets = new List<string>();
			string newSet = Json.Serializer.Serialize(new Dictionary<string, object> {
				{ "associatedChampions", new[] { champion } },
				{ "associatedMaps", Array.Empty<int>() },
				{ "blocks", blocks.ConvertAll(block => new Dictionary<string, object> {
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
			foreach (Json.Object itemSet in allSets) {
				string title = itemSet["title"].Get<string>();
				if (!title.EndsWith("(LCA)")) {
					foreignSets.Add(itemSet.ToString());
				} else if (!title.StartsWith(championName)) {
					ourSets.Add(itemSet.ToString());
				}
			}

			//Ensure capacity by removing a random item set from our sets if at capacity
			if (ourSets.Count >= Config.maxItemSets) {
				ourSets.RemoveAt(new Random().Next(ourSets.Count));
			}
			ourSets.AddRange(foreignSets);
			ourSets.Add(newSet);

			Client.Http.Response response = await Client.Http.PutJson($"/lol-item-sets/v1/item-sets/{Client.State.summonerId}/sets", $"{{\"accountId\":{setsJson["accountId"].Get<long>()},\"itemSets\":[{string.Join(",", ourSets)}],\"timestamp\":{setsJson["timestamp"].Get<long>()}}}");
			if (!response.Success) {
				//TODO: Starts failing at around 30 item sets due to the payload being too large.
				//Might be able to fix by moving to WebSockets or if there is a way to send only partial item set updates
				Console.WriteLine("Failed to update item sets");
			}
		}
	}
}
