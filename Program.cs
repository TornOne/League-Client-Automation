using System;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Threading.Tasks;

namespace LCA {
	// TODO:
	// More LolAlytics integration
	// - Fetch item sets
	// - - Compare item sets

	//http://www.mingweisamuel.com/lcu-schema/tool/
	static class Program {
		const string riotCert = "MIIEIDCCAwgCCQDJC+QAdVx4UDANBgkqhkiG9w0BAQUFADCB0TELMAkGA1UEBhMCVVMxEzARBgNVBAgTCkNhbGlmb3JuaWExFTATBgNVBAcTDFNhbnRhIE1vbmljYTETMBEGA1UEChMKUmlvdCBHYW1lczEdMBsGA1UECxMUTG9MIEdhbWUgRW5naW5lZXJpbmcxMzAxBgNVBAMTKkxvTCBHYW1lIEVuZ2luZWVyaW5nIENlcnRpZmljYXRlIEF1dGhvcml0eTEtMCsGCSqGSIb3DQEJARYeZ2FtZXRlY2hub2xvZ2llc0ByaW90Z2FtZXMuY29tMB4XDTEzMTIwNDAwNDgzOVoXDTQzMTEyNzAwNDgzOVowgdExCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpDYWxpZm9ybmlhMRUwEwYDVQQHEwxTYW50YSBNb25pY2ExEzARBgNVBAoTClJpb3QgR2FtZXMxHTAbBgNVBAsTFExvTCBHYW1lIEVuZ2luZWVyaW5nMTMwMQYDVQQDEypMb0wgR2FtZSBFbmdpbmVlcmluZyBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkxLTArBgkqhkiG9w0BCQEWHmdhbWV0ZWNobm9sb2dpZXNAcmlvdGdhbWVzLmNvbTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKoJemF/6PNG3GRJGbjzImTdOo1OJRDI7noRwJgDqkaJFkwv0X8aPUGbZSUzUO23cQcCgpYj21ygzKu5dtCN2EcQVVpNtyPuM2V4eEGr1woodzALtufL3Nlyh6g5jKKuDIfeUBHvJNyQf2h3Uha16lnrXmz9o9wsX/jf+jUAljBJqsMeACOpXfuZy+YKUCxSPOZaYTLCy+0GQfiT431pJHBQlrXAUwzOmaJPQ7M6mLfsnpHibSkxUfMfHROaYCZ/sbWKl3lrZA9DbwaKKfS1Iw0ucAeDudyuqb4JntGU/W0aboKA0c3YB02mxAM4oDnqseuKV/CX8SQAiaXnYotuNXMCAwEAATANBgkqhkiG9w0BAQUFAAOCAQEAf3KPmddqEqqC8iLslcd0euC4F5+USp9YsrZ3WuOzHqVxTtX3hR1scdlDXNvrsebQZUqwGdZGMS16ln3kWObw7BbhU89tDNCN7Lt/IjT4MGRYRE+TmRc5EeIXxHkQ78bQqbmAI3GsW+7kJsoOq3DdeE+M+BUJrhWorsAQCgUyZO166SAtKXKLIcxa+ddC49NvMQPJyzm3V+2b1roPSvD2WV8gRYUnGmy/N0+u6ANq5EsbhZ548zZc+BI4upsWChTLyxt2RxR7+uGlS1+5EcGfKZ+g024k/J32XP4hdho7WYAS2xMiV83CfLR/MNi8oSMaVQTdKD8cpgiWJk3LXWehWA==";

		static async Task Main() {
			System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

			Config.Load();
			Champion.Load();

			if (Config.launchGame) {
				Process.Start(Config.installPath + "LeagueClient.exe");
			}

			//Make sure the game is running and find the credentials
			string lockfilePath = Config.installPath + "lockfile";
			while (!File.Exists(lockfilePath)) {
				await Task.Delay(1000);
			}
			string[] credentials;
			using (StreamReader lockfileStream = new StreamReader(new FileStream(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128))) {
				credentials = lockfileStream.ReadToEnd().Split(':'); //[0]processname:[1]processid:[2]port:[3]password:[4]protocol
			}
			Console.WriteLine($"Found credentials - {string.Join(":", credentials)}");

			//(Hack way to) trust Riot's self-signed certificate
			System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => errors == SslPolicyErrors.None || //Either it's error-free
				errors == SslPolicyErrors.RemoteCertificateChainErrors && chain.ChainStatus.Length == 1 && chain.ChainStatus[0].Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot && //Or the problem is (only) in the chain having an untrusted root
				Convert.ToBase64String(chain.ChainElements[chain.ChainElements.Count - 1].Certificate.RawData) == riotCert; //which is the listed Riot certificate
			//Alternatively trust all certificates
			//ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;

			//Connect to the client via both HTTP and WebSocket
			await Client.Http.Initialize(credentials); //HTTP connection must be first, as WebSocket subscriptions can fail if the client isn't loaded, which we can't check there.
			await Client.WebSocket.Initialize(credentials);

			//Listen to user input
			await UserInput.ParseLoop();
		}
	}
}