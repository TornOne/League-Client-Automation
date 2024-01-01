namespace LCA;
readonly struct BanInfo(int id, double pbi) {
	public readonly int id = id;
	public readonly double pbi = pbi;

	public override string ToString() => $"{Champion.idToChampion[id].fullName,-12} - {pbi,2:0}";
}

readonly struct RankInfo(int rank, double wr, double delta) {
	public readonly int rank = rank;
	public readonly double wr = wr, delta = delta;

	public string ToString(int id) => $"{Champion.idToChampion[id].fullName,-12} - Rank {rank,3}, WR: {wr:0.0%} ({(delta >= 0 ? "+" : "")}{delta:0.0%})";
}