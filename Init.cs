﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace LCA;
static class Init {
	static async Task Main() {
		Config.Load();
		Champion.Load();

		if (Config.thirdPartyInterface == ThirdParty.API.LolAlytics) {
			ThirdParty.Interface.Initialize<ThirdParty.LolAlytics>();
		} else if (Config.thirdPartyInterface == ThirdParty.API.LolAlytics2) {
			ThirdParty.Interface.Initialize<ThirdParty.LolAlytics2>();
		}

		if (Config.launchGame) {
			Process.Start(Config.installPath + "LeagueClient.exe");
		}

		//Make sure the game is running
		DateTime leagueStartTime;
		while (true) {
			Process[] processes = Process.GetProcessesByName("LeagueClient");
			if (processes.Length > 0) {
				leagueStartTime = processes[0].StartTime;
				break;
			}
			await Task.Delay(1000);
		}

		//Find the credentials
		string lockfilePath = Config.installPath + "lockfile";
		while (!File.Exists(lockfilePath) || new FileInfo(lockfilePath).LastWriteTime < leagueStartTime) { //Check that we don't have an old lockfile from last time
			await Task.Delay(1000);
		}
		string[] credentials;
		using (StreamReader lockfileStream = new(new FileStream(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128))) {
			credentials = lockfileStream.ReadToEnd().Split(':'); //[0]processname:[1]processid:[2]port:[3]password:[4]protocol
		}
		Console.WriteLine($"Found credentials - {string.Join(":", credentials)}");

		//Connect to the client via both HTTP and WebSocket
		await Client.Http.Initialize(credentials); //HTTP connection must be first, as WebSocket subscriptions can fail if the client isn't loaded, which we can't check there.
		await Client.WebSocket.Initialize(credentials);

		//Listen to user input
		await UserInput.ParseLoop();
	}
}
