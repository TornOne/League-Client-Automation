using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace LCA;
class RunePage {
	public readonly struct Index(int category, int row, int column) {
		public readonly int category = category, row = row, column = column;
	}

	readonly int primaryStyle, subStyle;
	readonly int[] runes = new int[9];

	#region Constructors
	public RunePage(int[] ids) {
		primaryStyle = ids[0];
		subStyle = ids[1];
		Array.Copy(ids, 2, runes, 0, runes.Length);
	}

	public RunePage(int primaryStyle, int subStyle, ICollection<int> runes) {
		this.primaryStyle = primaryStyle;
		this.subStyle = subStyle;
		runes.CopyTo(this.runes, 0);
	}

	public RunePage(TextReader file) : this(Array.ConvertAll(file.ReadLine()!.Split(' '), int.Parse)) { }

	public void WriteToFile(TextWriter file) => file.WriteLine($"{primaryStyle} {subStyle} {string.Join(" ", runes)}");
	#endregion

	#region Constants
	public const int categoryCount = 5, rowCount = 4;
	public static readonly int[] styleIds = [ 8000, 8100, 8200, 8400, 8300 ]; //Precision, Domination, Sorcery, Resolve, Inspiration
	public static readonly int[,][] runeIds = new int[categoryCount, rowCount][] {
		{ //Precision
			[ 8005, 8008, 8021, 8010 ],
			[ 9101, 9111, 8009 ],
			[ 9104, 9105, 9103 ],
			[ 8014, 8017, 8299 ]
		}, { //Domination
			[ 8112, 8124, 8128, 9923 ],
			[ 8126, 8139, 8143 ],
			[ 8136, 8120, 8138 ],
			[ 8135, 8134, 8105, 8106 ]
		}, { //Sorcery
			[ 8214, 8229, 8230 ],
			[ 8224, 8226, 8275 ],
			[ 8210, 8234, 8233 ],
			[ 8237, 8232, 8236 ]
		}, { //Resolve
			[ 8437, 8439, 8465 ],
			[ 8446, 8463, 8401 ],
			[ 8429, 8444, 8473 ],
			[ 8451, 8453, 8242 ]
		}, { //Inspiration
			[ 8351, 8360, 8369 ],
			[ 8306, 8304, 8313 ],
			[ 8321, 8316, 8345 ],
			[ 8347, 8410, 8352 ]
		}
	};
	public static readonly Dictionary<int, Index> idToIndex = CreateIdToIndexMap();
	static Dictionary<int, Index> CreateIdToIndexMap() {
		Dictionary<int, Index> idToIndex = [];

		for (int category = 0; category < categoryCount; category++) {
			for (int row = 0; row < rowCount; row++) {
				int[] idRow = runeIds[category, row];
				for (int col = 0; col < idRow.Length; col++) {
					idToIndex[idRow[col]] = new Index(category, row, col);
				}
			}
		}

		return idToIndex;
	}
	public static double[][] KeystoneTemplate => new double[categoryCount][] { new double[runeIds[0, 0].Length], new double[runeIds[1, 0].Length], new double[runeIds[2, 0].Length], new double[runeIds[3, 0].Length], new double[runeIds[4, 0].Length] };
	public static double[,][] RuneTemplate => new double[categoryCount, rowCount - 1][] {
		{ new double[runeIds[0, 1].Length], new double[runeIds[0, 2].Length], new double[runeIds[0, 3].Length] },
		{ new double[runeIds[1, 1].Length], new double[runeIds[1, 2].Length], new double[runeIds[1, 3].Length] },
		{ new double[runeIds[2, 1].Length], new double[runeIds[2, 2].Length], new double[runeIds[2, 3].Length] },
		{ new double[runeIds[3, 1].Length], new double[runeIds[3, 2].Length], new double[runeIds[3, 3].Length] },
		{ new double[runeIds[4, 1].Length], new double[runeIds[4, 2].Length], new double[runeIds[4, 3].Length] }
	};
	#endregion

	#region Methods
	public static int[] GetBestRunes(double[][] keystones, double[,][] primary, double[,][] secondary) {
		(int[] ids, double totalValue)[] bestPrimaries = new (int[], double)[categoryCount];
		(int id1, int id2, double totalValue)[] bestSecondaries = new (int, int, double)[categoryCount];

		for (int category = 0; category < categoryCount; category++) {
			bestPrimaries[category].ids = new int[rowCount];
			for (int row = 0; row < rowCount; row++) {
				double value;
				(bestPrimaries[category].ids[row], value) = GetBestRuneInRow(runeIds[category, row], row == 0 ? keystones[category] : primary[category, row - 1]);
				bestPrimaries[category].totalValue += value;
			}

			(int id, double value)[] secondaries = new (int, double)[rowCount - 1];
			for (int row = 0; row < secondaries.Length; row++) {
				secondaries[row] = GetBestRuneInRow(runeIds[category, row + 1], secondary[category, row]);
			}
			Array.Sort(secondaries, (a, b) => b.value.CompareTo(a.value)); //Reverse order
			bestSecondaries[category] = (secondaries[0].id, secondaries[1].id, secondaries[0].value + secondaries[1].value);
		}

		double bestValue = 0;
		int[] bestRunes = new int[11];
		for (int primaryCategory = 0; primaryCategory < categoryCount; primaryCategory++) {
			for (int secondaryCategory = 0; secondaryCategory < categoryCount; secondaryCategory++) {
				if (primaryCategory == secondaryCategory) {
					continue;
				}

				double value = bestPrimaries[primaryCategory].totalValue + bestSecondaries[secondaryCategory].totalValue;
				if (value > bestValue) {
					bestValue = value;
					bestRunes[0] = styleIds[primaryCategory];
					bestRunes[1] = styleIds[secondaryCategory];
					Array.Copy(bestPrimaries[primaryCategory].ids, 0, bestRunes, 2, rowCount);
					bestRunes[6] = bestSecondaries[secondaryCategory].id1;
					bestRunes[7] = bestSecondaries[secondaryCategory].id2;
				}
			}
		}
		return bestRunes;
	}

	static (int id, double value) GetBestRuneInRow(int[] ids, double[] values) {
		int bestRune = 0;
		double bestValue = 0;
		for (int column = 0; column < values.Length; column++) {
			if (values[column] > bestValue) {
				bestValue = values[column];
				bestRune = ids[column];
			}
		}
		return (bestRune, bestValue);
	}

	public static async Task Free(int amount) {
		int maxPages = (await Client.Http.Get("/lol-perks/v1/inventory")).AsJson()!.RootElement.GetProperty("ownedPageCount").GetInt32();
		JsonElement pages = (await Client.Http.Get("/lol-perks/v1/pages")).AsJson()!.RootElement;

		for (int i = maxPages - amount; i < pages.GetArrayLength(); i++) {
			await Client.Http.Delete("/lol-perks/v1/pages/" + pages[i].GetProperty("id").GetInt32());
		}
	}

	public async Task<bool> CreateRunePage(string pageName) => (await Client.Http.PostJson("/lol-perks/v1/pages", Torn.Json.Serializer.Serialize(new Dictionary<string, object> {
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
		{ "primaryStyleId", primaryStyle },
		{ "subStyleId", subStyle },
		{ "selectedPerkIds", runes }
	}))).Success;

	public int Compare(RunePage other) {
		int differingRuneCount = 0;
		for (int i = 0; i < 6; i++) {
			if (!Array.Exists(runes, rune => rune == other.runes[i])) {
				differingRuneCount++;
			}
		}
		for (int i = 6; i < 9; i++) {
			if (runes[i] != other.runes[i]) {
				differingRuneCount++;
			}
		}
		return differingRuneCount;
	}
	#endregion
}