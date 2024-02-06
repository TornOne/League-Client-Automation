<div align="justify">

# About
League Client Automation is a command line tool designed to assist you during champion select to make better choices and reduce the tedium of re-making your rune page every game.  
It integrates with a third-party site to fetch up-to-date statistics on champions, and uses a little bit of math to give you good suggestions. Ultimately, these are just suggestions, most features can be turned off, and you will always have the final say on what you do.  
As of the time of writing, tools like this are okay to use in League, since they only assist you before the game begins. I will never include anything that I know might break the rules. Still, use at your own risk.

Current features include:
* Saving preset runes per champion, lane, and game mode to be loaded upon locking in.
* Fetching meta runes, summoner spells, and items per champion, lane, and game mode.
* Suggesting bans for the team and for your lane.
* Showing ranking (win rate) data for your team's champions in ARAM and ARURF.

Potential future features include:
* Counterpick suggestions.

# Setup
* Download the zip file from the [latest release](https://github.com/TornOne/League-Client-Automation/releases/latest).
* Unzip it to a not write-protected location of your choosing, and run `Patcher.exe`.  
Note: The program does not automatically update. You may occasionally run Patcher.exe again to check if there have been any updates.
* Open `config.json` and edit the `installPath` value to point to your League installation folder.  
Note: It is recommended to use forward slashes (`/`) in the path. Backslashes also work, but must be escaped by another backslash (`\\`).  
You may look over the other configuration options for further customization before running the program.

# Usage
Run `League Client Automation.exe` before or after opening up the League client.  
You should get some lines of information as it is starting up (as well as some errors if you started it before opening League - this is normal). The program will tell you when it is ready to use.  
You can type `help` to get a short reminder of possible commands.

The program runs without user intervention for most of the time, but it is possible to save rune page presets as well as load champions manually (for example, if the automatic loading messes up or you decide to play a different lane than the one you were assigned).  
The save/load/delete syntax is `save/load/delete champion lane`. Specifying the lane is optional. The "Default" lane will be used if the lane is not specfied. Both champion and lane names may be partial. Example: `load ez bot`  
`list` will show all the champions and lanes you have saved rune pages for.  
For saving preset rune pages, first create a new rune page or edit an existing one. Upon clicking "SAVE" in the in-game UI, you will be notified with a "Runes updated" message, indicating the program has remembered this rune page. You can then save this rune page to a champion e.g. `save lux`. When you next play / load this champion (in a matching lane), the saved rune page will be loaded alongside the LolAlytics page, prioritizing the saved page.

The numbers displayed in automatic item set block names are, to put it simply, relative adjusted win rates. Bigger is better, but keep an open mind about what items you actually need at the time. A small superscript number after the win rate indicates how much less popular that item is compared to the most popular item for that block. The values are roughly: 1 = 1.4x...2.8x, 2 = 2.8x...5.6x, 3 = >5.6x less popular. A higher number on a highly ranked item may indicate the item is somewhat situational or that it is a more powerful pick which people are less aware of.  
The Situational items block overprioritizes win rates to show you items which win very often when picked, but may not be picked very often. It might be helpful to pick something from them for your last item or two, and/or if you feel some of them could counter the enemy team well. But do not pick from these if you can not justify why the pick would be a good item.

# Configuration
The following is a list of all the configuration options along with explanations and possible values.
* `installPath`: A string indicating the absolute path to the League installation folder. More specifically, the folder in which `LeagueClient.exe` is contained.
* `launchGame`: A boolean (`true`/`false`) value. If `true`, will attempt to launch the League client itself, saving the trouble of having to open both programs separately.
* `setSummonerSpells`: A boolean value. If `true`, will automatically set your Summoner Spells to the best combination fetched from the third-party site.
* `spellOrder`: A list of strings representing the Summoner Spell names. When automatically setting your Summoner Spells, the spell that is earlier (higher) on the list will be placed on the left.  
Rearrange them to make sure your Summoner Spells will be assigned to the correct key. Do not add or remove any values. Do not forget that the last value must not have a comma after it, while the other values must have a comma after them.
* `banSuggestions`: An integer value. Will show the specified number of ban suggestions at the beginning of a Draft lobby for both all lanes, and the lane you are playing. Setting it to 0 will disable this feature.
* `eventBanSuggestions`: Same as `banSuggestions` but for event game modes that have a ban phase. Only shows the overall ban priorities, not lane specific bans.
* `maxItemSets`: An integer value. Will use up to this many item set slots for automatic item sets. The rest are left for manual item sets. Having over 30 item sets total (manual + automatic) may cause automatic item sets to fail. Setting it to 0 will disable this feature.
* `minGamesChamp`: An integer value. At least this many games must be present for this champion, lane, and rank combination to use the available data. Can set it to 0 to disable it, or set it higher if you'd rather make your own choices than rely on questionable amounts of data.  
Values <100 allow for effectively useless and near-random choices. 100-1000 has a high chance of having weird choices, but is mostly fine. >1000 is usually entirely fine.
* `thirdPartyInterface`: An integer value specifying which third-party site to use for data for suggestions. A value of `1` uses LolAlytics. Any other value effectively disables all functionality of the program except for preset rune pages.
* `openThirdPartySite`: A boolean value. If `true`, will open the third-party page for the champion when you lock in.
* `lolAlyticsQueueRankMap`: A list of game mode names followed by a rank name. When fetching LolAlytics data and opening the page, the rank correspondng to the current game mode will be used.  
The possible rank values are: `unranked`, `iron`, `bronze`, `silver`, `gold`, `platinum`, `emerald`, `diamond`, `master`, `grandmaster`, `challenger`, `all`, `gold_plus`, `platinum_plus`, `emerald_plus`, `diamond_plus`, `d2_plus`, `master_plus`.  
There is no guarantee that every option will have sufficient data for the optimal functioning of the program, especially after a new patch has recently been released. I would personally recommend using the defaults, using "all", or using one of the "plus" ranks up to diamond.

</div>
