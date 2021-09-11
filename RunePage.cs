using System;
using System.Collections.Generic;
using System.IO;

class RunePage {
	public readonly int primaryStyle, subStyle;
	public readonly int[] runes = new int[9];

	public RunePage(int[] ids) {
		primaryStyle = ids[0];
		subStyle = ids[1];
		Array.Copy(ids, 2, runes, 0, 9);
	}

	public RunePage(int primaryStyle, int subStyle, ICollection<int> runes) {
		this.primaryStyle = primaryStyle;
		this.subStyle = subStyle;
		runes.CopyTo(this.runes, 0);
	}

	public RunePage(TextReader file) : this(Array.ConvertAll(file.ReadLine().Split(' '), id => int.Parse(id))) { }

	public void WriteToFile(TextWriter file) => file.WriteLine($"{primaryStyle} {subStyle} {string.Join(" ", runes)}");

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
	public static readonly Dictionary<(int category, int row, int column), int> templateIndexToId = new Dictionary<(int category, int row, int column), int>(idToTemplateIndex.Count);
	public static (double[][] keystone, double[,][] secondary) PrimaryTemplate => (new double[5][] { new double[4], new double[4], new double[3], new double[3], new double[3] }, SecondaryTemplate);
	public static double[,][] SecondaryTemplate => new double[5, 3][] {
		{ new double[3], new double[3], new double[3] },
		{ new double[3], new double[3], new double[4] },
		{ new double[3], new double[3], new double[3] },
		{ new double[3], new double[3], new double[3] },
		{ new double[3], new double[3], new double[3] }
	};

	static RunePage() {
		foreach (KeyValuePair<int, (int category, int row, int column)> rune in idToTemplateIndex) {
			templateIndexToId[rune.Value] = rune.Key;
		}
	}
}
