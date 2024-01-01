using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Torn.Json;

namespace LCA.Client;
static class Http {
	public class Response {
		public readonly int code;
		public bool Success => code >= 200 && code < 300;
		public readonly string content;
		public JsonDocument? AsJson() {
			if (!Success) {
				return null;
			}

			try {
				return JsonDocument.Parse(content);
			} catch {
				return null;
			}
		}

		Response(int code, string content) {
			this.code = code;
			this.content = content;
		}

		public static async Task<Response> FromResponseTask(Task<HttpResponseMessage> responseTask) {
			try {
				HttpResponseMessage response = await responseTask;
				return new Response((int)response.StatusCode, await response.Content.ReadAsStringAsync());
			} catch (Exception ex) {
				return new Response(-1, ex.Message);
			}
		}
	}

	static readonly HttpClient client = new(new SocketsHttpHandler { SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = ValidateCert } });

	public static async Task Initialize(string[] credentials) {
		client.BaseAddress = new Uri($"https://127.0.0.1:{credentials[2]}");
		client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Array.ConvertAll($"riot:{credentials[3]}".ToCharArray(), c => (byte)c)));

		//Fetch summoner ID and game version to confirm the client is working
		static Task PrintFailure(string text) {
			Console.WriteLine($"Initial HTTP request failed, retrying - {text}");
			return Task.Delay(3000);
		}
		while (true) {
			//Summoner ID
			Response response = await Get("/lol-summoner/v1/current-summoner");
			JsonDocument? jsonDoc = response.AsJson();
			if (jsonDoc is null || !jsonDoc.RootElement.TryGetValue("summonerId", out JsonElement summonerId) || !summonerId.TryGetValue(out State.summonerId)) {
				await PrintFailure(response.content);
				continue;
			}
			jsonDoc?.Dispose();

			//Game version
			response = await Get("/lol-patch/v1/game-version");
			jsonDoc = response.AsJson();
			if (jsonDoc is null || !jsonDoc.RootElement.TryGetValue(out string version)) {
				await PrintFailure(response.content);
				continue;
			}
			string[] versionParts = version.Split([ '.' ], 3);
			if (!int.TryParse(versionParts[0], out State.gameVersionMajor) || !int.TryParse(versionParts[1], out State.gameVersionMinor)) {
				await PrintFailure(response.content);
				continue;
			}
			jsonDoc?.Dispose();

			break;
		}
		Console.WriteLine($"Game version {State.gameVersionMajor}.{State.gameVersionMinor}");
		Console.WriteLine($"Logged in as summoner {State.summonerId}");
	}

	public static Task<Response> Get(string uri) => Response.FromResponseTask(client.GetAsync(uri));
	public static Task<Response> Delete(string uri) => Response.FromResponseTask(client.DeleteAsync(uri));
	public static Task<Response> PutJson(string uri, string content) => SendJson("PUT", uri, content);
	public static Task<Response> PostJson(string uri, string content) => SendJson("POST", uri, content);
	public static Task<Response> PatchJson(string uri, string content) => SendJson("PATCH", uri, content);

	static Task<Response> SendJson(string method, string uri, string content) => Response.FromResponseTask(client.SendAsync(new HttpRequestMessage(new HttpMethod(method), uri) {
		Content = new StringContent(content, Encoding.UTF8, "application/json")
	}));

	const string riotCert = "MIIEIDCCAwgCCQDJC+QAdVx4UDANBgkqhkiG9w0BAQUFADCB0TELMAkGA1UEBhMCVVMxEzARBgNVBAgTCkNhbGlmb3JuaWExFTATBgNVBAcTDFNhbnRhIE1vbmljYTETMBEGA1UEChMKUmlvdCBHYW1lczEdMBsGA1UECxMUTG9MIEdhbWUgRW5naW5lZXJpbmcxMzAxBgNVBAMTKkxvTCBHYW1lIEVuZ2luZWVyaW5nIENlcnRpZmljYXRlIEF1dGhvcml0eTEtMCsGCSqGSIb3DQEJARYeZ2FtZXRlY2hub2xvZ2llc0ByaW90Z2FtZXMuY29tMB4XDTEzMTIwNDAwNDgzOVoXDTQzMTEyNzAwNDgzOVowgdExCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpDYWxpZm9ybmlhMRUwEwYDVQQHEwxTYW50YSBNb25pY2ExEzARBgNVBAoTClJpb3QgR2FtZXMxHTAbBgNVBAsTFExvTCBHYW1lIEVuZ2luZWVyaW5nMTMwMQYDVQQDEypMb0wgR2FtZSBFbmdpbmVlcmluZyBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkxLTArBgkqhkiG9w0BCQEWHmdhbWV0ZWNobm9sb2dpZXNAcmlvdGdhbWVzLmNvbTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKoJemF/6PNG3GRJGbjzImTdOo1OJRDI7noRwJgDqkaJFkwv0X8aPUGbZSUzUO23cQcCgpYj21ygzKu5dtCN2EcQVVpNtyPuM2V4eEGr1woodzALtufL3Nlyh6g5jKKuDIfeUBHvJNyQf2h3Uha16lnrXmz9o9wsX/jf+jUAljBJqsMeACOpXfuZy+YKUCxSPOZaYTLCy+0GQfiT431pJHBQlrXAUwzOmaJPQ7M6mLfsnpHibSkxUfMfHROaYCZ/sbWKl3lrZA9DbwaKKfS1Iw0ucAeDudyuqb4JntGU/W0aboKA0c3YB02mxAM4oDnqseuKV/CX8SQAiaXnYotuNXMCAwEAATANBgkqhkiG9w0BAQUFAAOCAQEAf3KPmddqEqqC8iLslcd0euC4F5+USp9YsrZ3WuOzHqVxTtX3hR1scdlDXNvrsebQZUqwGdZGMS16ln3kWObw7BbhU89tDNCN7Lt/IjT4MGRYRE+TmRc5EeIXxHkQ78bQqbmAI3GsW+7kJsoOq3DdeE+M+BUJrhWorsAQCgUyZO166SAtKXKLIcxa+ddC49NvMQPJyzm3V+2b1roPSvD2WV8gRYUnGmy/N0+u6ANq5EsbhZ548zZc+BI4upsWChTLyxt2RxR7+uGlS1+5EcGfKZ+g024k/J32XP4hdho7WYAS2xMiV83CfLR/MNi8oSMaVQTdKD8cpgiWJk3LXWehWA==";

	public static bool ValidateCert(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => errors == SslPolicyErrors.None || //Either it's error-free
		errors == SslPolicyErrors.RemoteCertificateChainErrors && chain?.ChainStatus.Length == 1 && chain.ChainStatus[0].Status == X509ChainStatusFlags.UntrustedRoot && //Or the problem is (only) in the chain having an untrusted root
		Convert.ToBase64String(chain.ChainElements[^1].Certificate.RawData) == riotCert; //which is the listed Riot certificate
}