namespace LCA;
//https://raw.communitydragon.org/12.11/plugins/rcp-be-lol-game-data/global/default/v1/queues.json
//https://static.developer.riotgames.com/docs/lol/queues.json
enum Lane {
	Default,
	Top,
	Jungle,
	Middle,
	Bottom,
	Support,
	ARAM = 450,
	ARURF = 900,
	OneForAll = 1020,
	Nexus = 1300,
	UltimateSpellBook = 1400,
	Arena = 1700,
	URF = 1900
}

//http://ddragon.leagueoflegends.com/cdn/12.11.1/data/en_US/summoner.json
enum Spell {
	Cleanse = 1,
	Exhaust = 3,
	Flash = 4,
	Ghost = 6,
	Heal = 7,
	Smite = 11,
	Teleport = 12,
	Clarity = 13,
	Ignite = 14,
	Barrier = 21,
	Mark = 32,
	Placeholder = 54
}
