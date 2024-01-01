using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LCA.ThirdParty;

interface IInterface {
	abstract static Task Initialize();
	abstract static Task<BanInfo[]> GetBanSuggestions(Lane lane);
	abstract static Task<Dictionary<int, RankInfo>> GetRanks(Lane queue);
	abstract static Task<Data?> FetchData(Lane lane, int championId);
}

static class Interface {
	public static Func<Lane, Task<BanInfo[]>> GetBanSuggestions = _ => Task.FromResult(Array.Empty<BanInfo>());
	public static Func<Lane, Task<Dictionary<int, RankInfo>>> GetRanks = _ => Task.FromResult(new Dictionary<int, RankInfo>());
	public static Func<Lane, int, Task<Data?>> FetchData = (_, _) => Task.FromResult<Data?>(null);

	public static void Initialize<T>() where T : IInterface {
		T.Initialize();
		GetBanSuggestions = T.GetBanSuggestions;
		GetRanks = T.GetRanks;
		FetchData = T.FetchData;
	}
}

class Data(string? url, string skillOrder, string firstSkills, int spell1Id, int spell2Id, RunePage runePage, ItemSet itemSet) {
	public readonly string? url = url;
	public readonly string skillOrder = skillOrder, firstSkills = firstSkills;
	public readonly int spell1Id = spell1Id, spell2Id = spell2Id;
	public readonly RunePage runePage = runePage;
	public readonly ItemSet itemSet = itemSet;
}

enum API {
	None,
	LolAlytics
}
