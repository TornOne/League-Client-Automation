using System.Collections.Generic;

namespace LCA.Client {
	static class State {
		public static long summonerId;
		public static string currentVersion;
		public static RunePage lastRunes;

		public static HashSet<int> ourChampions = new HashSet<int>();
		public static Champion currentChampion;
		public static Lane currentLane;
		public static bool modeHasBench;

		public static void Reset() {
			ourChampions.Clear();
			currentChampion = null;
			currentLane = Lane.Default;
			modeHasBench = false;
		}
	}
}