using System;
using System.Collections.Generic;
using System.IO;

class RunePage {
	public readonly int primaryStyle, subStyle;
	public readonly int[] runes = new int[9];

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

	public RunePage(TextReader file) : this(Array.ConvertAll(file.ReadLine().Split(' '), id => int.Parse(id))) { }

	public void WriteToFile(TextWriter file) => file.WriteLine($"{primaryStyle} {subStyle} {string.Join(" ", runes)}");

	public const int categoryCount = 5, rowCount = 4;
	public static readonly int[] styleIds = new int[categoryCount] { 8000, 8100, 8200, 8400, 8300 }; //Precision, Domination, Sorcery, Resolve, Inspiration
	public static readonly int[,][] runeIds = new int[categoryCount, rowCount][] {
		{ //Precision
			new[] { 8005, 8008, 8021, 8010 },
			new[] { 9101, 9111, 8009 },
			new[] { 9104, 9105, 9103 },
			new[] { 8014, 8017, 8299 }
		}, { //Domination
			new[] { 8112, 8124, 8128, 9923 },
			new[] { 8126, 8139, 8143 },
			new[] { 8136, 8120, 8138 },
			new[] { 8135, 8134, 8105, 8106 }
		}, { //Sorcery
			new[] { 8214, 8229, 8230 },
			new[] { 8224, 8226, 8275 },
			new[] { 8210, 8234, 8233 },
			new[] { 8237, 8232, 8236 }
		}, { //Resolve
			new[] { 8437, 8439, 8465 },
			new[] { 8446, 8463, 8401 },
			new[] { 8429, 8444, 8473 },
			new[] { 8451, 8453, 8242 }
		}, { //Inspiration
			new[] { 8351, 8360, 8369 },
			new[] { 8306, 8304, 8313 },
			new[] { 8321, 8316, 8345 },
			new[] { 8347, 8410, 8352 }
		}
	};
	public static readonly Dictionary<int, (int category, int row, int column)> idToTemplateIndex = CreateIdToTemplateIndexMap();
	static Dictionary<int, (int, int, int)> CreateIdToTemplateIndexMap() {
		Dictionary<int, (int, int, int)> idToTemplateIndex = new Dictionary<int, (int, int, int)>();

		for (int category = 0; category < categoryCount; category++) {
			for (int row = 0; row < rowCount; row++) {
				int[] idRow = runeIds[category, row];
				for (int col = 0; col < idRow.Length; col++) {
					idToTemplateIndex[idRow[col]] = (category, row, col);
				}
			}
		}

		return idToTemplateIndex;
	}
	public static double[][] KeystoneTemplate => new double[categoryCount][] { new double[runeIds[0, 0].Length], new double[runeIds[1, 0].Length], new double[runeIds[2, 0].Length], new double[runeIds[3, 0].Length], new double[runeIds[4, 0].Length] };
	public static double[,][] RuneTemplate => new double[categoryCount, rowCount - 1][] {
		{ new double[runeIds[0, 1].Length], new double[runeIds[0, 2].Length], new double[runeIds[0, 3].Length] },
		{ new double[runeIds[1, 1].Length], new double[runeIds[1, 2].Length], new double[runeIds[1, 3].Length] },
		{ new double[runeIds[2, 1].Length], new double[runeIds[2, 2].Length], new double[runeIds[2, 3].Length] },
		{ new double[runeIds[3, 1].Length], new double[runeIds[3, 2].Length], new double[runeIds[3, 3].Length] },
		{ new double[runeIds[4, 1].Length], new double[runeIds[4, 2].Length], new double[runeIds[4, 3].Length] }
	};
}
