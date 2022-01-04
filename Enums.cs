namespace LCA {
	enum Lane {
		Default,
		Top,
		Jungle,
		Middle,
		Bottom,
		Support,
		ARAM = 450,
		URF = 900,
		OneForAll = 1020,
		Nexus = 1300,
		UltimateSpellBook = 1400
	}

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
		Placeholder = 54,
	}

	enum Rank {
		unranked,
		iron,
		bronze,
		silver,
		gold,
		platinum,
		diamond,
		master,
		grandmaster,
		challenger,
		all,
		gold_plus,
		platinum_plus,
		diamond_plus,
		d2_plus,
		master_plus
	}
}