namespace LCA {
	readonly struct BanInfo {
		public readonly int id;
		public readonly double pbi;

		public BanInfo(int id, double pbi) {
			this.id = id;
			this.pbi = pbi;
		}

		public override string ToString() => $"{Champion.idToChampion[id].fullName,-12} - {pbi,2:0}";
	}

	readonly struct RankInfo {
		public readonly int rank;
		public readonly double wr, delta;

		public RankInfo(int rank, double wr, double delta) {
			this.rank = rank;
			this.wr = wr;
			this.delta = delta;
		}

		public string ToString(int id) => $"{Champion.idToChampion[id].fullName,-12} - Rank {rank,3}, WR: {wr:0.0%} ({(delta >= 0 ? "+" : "")}{delta:0.0%})";
	}
}
