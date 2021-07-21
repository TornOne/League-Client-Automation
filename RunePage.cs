using System;
using System.Collections.Generic;
using System.IO;

class RunePage {
	readonly int[] ids = new int[11];
	public int PrimaryStyle => ids[0];
	public int SubStyle => ids[1];
	public readonly int[] runes = new int[9];

	public RunePage(int[] ids) {
		ids.CopyTo(this.ids, 0);
		Array.Copy(this.ids, 2, runes, 0, 9);
	}

	public RunePage(int primaryStyle, int subStyle, ICollection<int> runes) {
		ids[0] = primaryStyle;
		ids[1] = subStyle;
		runes.CopyTo(this.runes, 0);
		runes.CopyTo(ids, 2);
	}

	public RunePage(TextReader file) {
		ids = Array.ConvertAll(file.ReadLine().Split(' '), id => int.Parse(id));
		Array.Copy(ids, 2, runes, 0, 9);
	}

	public void WriteToFile(TextWriter file) => file.WriteLine(string.Join(" ", ids));

	public static readonly int[] styleIds = new[] { 8000, 8100, 8200, 8400, 8300 }; //Precision, Domination, Sorcery, Resolve, Inspiration
	public static readonly Dictionary<int, (int category, int row, int column)> idToTemplateIndex = new Dictionary<int, (int, int, int)> {
		//Precision
		{ 8005, (0, -1, 0) }, { 8008, (0, -1, 1) }, { 8021, (0, -1, 2) }, { 8010, (0, -1, 3) },
		{ 9101, (0, 0, 0) }, { 9111, (0, 0, 1) }, { 8009, (0, 0, 2) },
		{ 9104, (0, 1, 0) }, { 9105, (0, 1, 1) }, { 9103, (0, 1, 2) },
		{ 8014, (0, 2, 0) }, { 8017, (0, 2, 1) }, { 8299, (0, 2, 2) },
		//Domination
		{ 8112, (1, -1, 0) }, { 8124, (1, -1, 1) }, { 8128, (1, -1, 2) }, { 9923, (1, -1, 3) },
		{ 8126, (1, 0, 0) }, { 8139, (1, 0, 1) }, { 8143, (1, 0, 2) },
		{ 8136, (1, 1, 0) }, { 8120, (1, 1, 1) }, { 8138, (1, 1, 2) },
		{ 8135, (1, 2, 0) }, { 8134, (1, 2, 1) }, { 8105, (1, 2, 2) }, { 8106, (1, 2, 3) },
		//Sorcery
		{ 8214, (2, -1, 0) }, { 8229, (2, -1, 1) }, { 8230, (2, -1, 2) },
		{ 8224, (2, 0, 0) }, { 8226, (2, 0, 1) }, { 8275, (2, 0, 2) },
		{ 8210, (2, 1, 0) }, { 8234, (2, 1, 1) }, { 8233, (2, 1, 2) },
		{ 8237, (2, 2, 0) }, { 8232, (2, 2, 1) }, { 8236, (2, 2, 2) },
		//Resolve
		{ 8437, (3, -1, 0) }, { 8439, (3, -1, 1) }, { 8465, (3, -1, 2) },
		{ 8446, (3, 0, 0) }, { 8463, (3, 0, 1) }, { 8401, (3, 0, 2) },
		{ 8429, (3, 1, 0) }, { 8444, (3, 1, 1) }, { 8473, (3, 1, 2) },
		{ 8451, (3, 2, 0) }, { 8453, (3, 2, 1) }, { 8242, (3, 2, 2) },
		//Inspiration
		{ 8351, (4, -1, 0) }, { 8360, (4, -1, 1) }, { 8358, (4, -1, 2) },
		{ 8306, (4, 0, 0) }, { 8304, (4, 0, 1) }, { 8313, (4, 0, 2) },
		{ 8321, (4, 1, 0) }, { 8316, (4, 1, 1) }, { 8345, (4, 1, 2) },
		{ 8347, (4, 2, 0) }, { 8410, (4, 2, 1) }, { 8352, (4, 2, 2) }
	};
	public static readonly Dictionary<(int category, int row, int column), int> templateIndexToId = new Dictionary<(int category, int row, int column), int> {
		//Precision
		{ (0, -1, 0), 8005 }, { (0, -1, 1), 8008 }, { (0, -1, 2), 8021 }, { (0, -1, 3), 8010 },
		{ (0, 0, 0), 9101 }, { (0, 0, 1), 9111 }, { (0, 0, 2), 8009 },
		{ (0, 1, 0), 9104 }, { (0, 1, 1), 9105 }, { (0, 1, 2), 9103 },
		{ (0, 2, 0), 8014 }, { (0, 2, 1), 8017 }, { (0, 2, 2), 8299 },
		//Domination
		{ (1, -1, 0), 8112 }, { (1, -1, 1), 8124 }, { (1, -1, 2), 8128 }, { (1, -1, 3), 9923 },
		{ (1, 0, 0), 8126 }, { (1, 0, 1), 8139 }, { (1, 0, 2), 8143 },
		{ (1, 1, 0), 8136 }, { (1, 1, 1), 8120 }, { (1, 1, 2), 8138 },
		{ (1, 2, 0), 8135 }, { (1, 2, 1), 8134 }, { (1, 2, 2), 8105 }, { (1, 2, 3), 8106 },
		//Sorcery
		{ (2, -1, 0), 8214 }, { (2, -1, 1), 8229 }, { (2, -1, 2), 8230 },
		{ (2, 0, 0), 8224 }, { (2, 0, 1), 8226 }, { (2, 0, 2), 8275 },
		{ (2, 1, 0), 8210 }, { (2, 1, 1), 8234 }, { (2, 1, 2), 8233 },
		{ (2, 2, 0), 8237 }, { (2, 2, 1), 8232 }, { (2, 2, 2), 8236 },
		//Resolve
		{ (3, -1, 0), 8437 }, { (3, -1, 1), 8439 }, { (3, -1, 2), 8465 },
		{ (3, 0, 0), 8446 }, { (3, 0, 1), 8463 }, { (3, 0, 2), 8401 },
		{ (3, 1, 0), 8429 }, { (3, 1, 1), 8444 }, { (3, 1, 2), 8473 },
		{ (3, 2, 0), 8451 }, { (3, 2, 1), 8453 }, { (3, 2, 2), 8242 },
		//Inspiration
		{ (4, -1, 0), 8351 }, { (4, -1, 1), 8360 }, { (4, -1, 2), 8358 },
		{ (4, 0, 0), 8306 }, { (4, 0, 1), 8304 }, { (4, 0, 2), 8313 },
		{ (4, 1, 0), 8321 }, { (4, 1, 1), 8316 }, { (4, 1, 2), 8345 },
		{ (4, 2, 0), 8347 }, { (4, 2, 1), 8410 }, { (4, 2, 2), 8352 }
	};
	public static (int[][,] keystone, int[,][,] secondary) PrimaryTemplate => (new int[5][,] { new int[4, 2], new int[4, 2], new int[3, 2], new int[3, 2], new int[3, 2] }, SecondaryTemplate);
	public static int[,][,] SecondaryTemplate => new int[5, 3][,] {
		{ new int[3, 2], new int[3, 2], new int[3, 2] },
		{ new int[3, 2], new int[3, 2], new int[4, 2] },
		{ new int[3, 2], new int[3, 2], new int[3, 2] },
		{ new int[3, 2], new int[3, 2], new int[3, 2] },
		{ new int[3, 2], new int[3, 2], new int[3, 2] }
	};
}
