# About
League Client Automation is a command line tool designed to assist you during champion select to make better choices and reduce the tedium of re-making your rune page every game.  
It integrates with LolAlytics to fetch up-to-date statistics on champions, and uses a little bit of math to give you good suggestions. Ultimately, these are just suggestions, most features can be turned off, and you will always have the final say on what you do.  
As of the time of writing, tools like this are okay to use in League, since they only assist you before the game begins. I will never include anything that I know might break the rules. Still, use at your own risk.

Current features include:
* Saving preset runes per champion, lane, and game mode to be loaded upon locking in.
* Fetching meta runes and summoner spells per champion, lane, and game mode.
* Suggesting bans for the team and for your lane.
* Showing ranking (win rate) data for your team's champions in ARAM and ARURF.

Potential future features include:
* Fetching meta item builds.
* Counterpick suggestions.

# Setup
* Navigate under Releases and download the 7z file from the latest release.
* Unzip it to a not write-protected location of your choosing, and run `Patcher.exe`.  
Note: The program does not automatically update. You may occasionally run Patcher.exe again to check if there have been any updates.
* Open `config.json` and edit the `installPath` value to point to your League installation folder.  
Note: It is recommended to use forward slashes (`/`) in the path. Backslashes also work, but must be escaped by another backslash (`\\`).  
You may look over the other configuration options for further customization before running the program.

# Usage
Run `League Client Automation.exe` before or after opening up the League client.  
You should get some lines of information as it is starting up (as well as some errors if you started it before opening League - this is normal). The program should be operational once it tells you the game version, your summoner ID, and "Connected to client".  
You can type `help` to get an explanation of possible commands. I will not be duplicating the full help text here.  
The program runs without user intervention for most of the time, but it is possible to save rune page presets as well as load champions manually (for example, if the automatic loading messes up or you decide to play a different lane than the one you were assigned).  
The save/load syntax is `save/load champion lane`. Specifying the lane is optional. Both champion and lane names may be partial. Example: `load ez bot`  
For saving preset rune pages, first create a new rune page or edit an existing one. Upon clicking "SAVE" in the in-game UI, you will be notified with a "Runes updated" message, indicating the program has remembered this rune page. You can then save this rune page to a champion e.g. `save lux`. When you next play / load this champion (in a matching lane), the saved rune page will be loaded alongside the LolAlytics page, prioritizing the saved page.

# Configuration
The following is a list of all the configuration options along with explanations and possible values.
* `installPath`: A string indicating the absolute path to the League installation folder. More specifically, the folder in which `LeagueClient.exe` is contained.
* `launchGame`: A boolean (`true`/`false`) value. If `true`, will attempt to launch the League client itself, saving the trouble of having to open both programs separately.
* `openLolAlytics`: A boolean value. If `true`, will open the LolAlytics page of the champion when you lock in.
* `setSummonerSpells`: A boolean value. If `true`, will automatically set your Summoner Spells to the best combination fetched from LolAlytics.
* `spellOrder`: A list of strings representing the Summoner Spell names. When automatically setting your Summoner Spells, the spell that is earlier (higher) on the list will be placed on the left.  
Rearrange them to make sure your Summoner Spells will be assigned to the correct key. Do not add or remove any values. Do not forget that the last value must not have a comma after it, while the other values must have a comma after them.
* `queueRankMap`: A list of game mode names followed by a rank name. When fetching LolAlytics data and opening the page, the rank correspondng to the current game mode will be used.  
The possible rank values are: `unranked`, `iron`, `bronze`, `silver`, `gold`, `platinum`, `diamond`, `master`, `grandmaster`, `challenger`, `all`, `gold_plus`, `platinum_plus`, `diamond_plus`, `d2_plus`, `master_plus`.  
There is no guarantee that all ranks will have sufficient data for the optimal functioning of the program, especially after a new patch has recently been released. I would personally recommend using the defaults, or using "all", or one of the "plus" ranks up to diamond.
* `banSuggestions`: An integer value. Will show the specified number of ban suggestions at the beginning of a Draft lobby for both all lanes, and the lane you are playing. Setting it to 0 will disable this feature.
