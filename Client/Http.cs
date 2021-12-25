using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LCA.Client {
	//TODO: Make this more robust. Namely, the public methods should return associated data of the response, like the error code, and whether it was a success, instead of crashing on fail.
	static class Http {
		public class Response {
			public readonly int code;
			public bool Success => code >= 200 && code < 300;
			public readonly string content;

			Response(int code, string content) {
				this.code = code;
				this.content = content;
			}

			public static async Task<Response> FromResponse(HttpResponseMessage response) => new Response((int)response.StatusCode, await response.Content.ReadAsStringAsync());
		}

		static HttpClient client;

		public static async Task Initialize(string[] credentials) {
			client = new HttpClient {
				BaseAddress = new Uri($"https://127.0.0.1:{credentials[2]}")
			};
			client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Array.ConvertAll($"riot:{credentials[3]}".ToCharArray(), c => (byte)c)));

			//Fetch Summoner ID
			while (true) {
				try {
					State.summonerId = (await GetJson("/lol-summoner/v1/current-summoner"))["summonerId"].Get<long>();
					string[] version = (await GetJson("/lol-patch/v1/game-version")).Get<string>().Split(new[] { '.' }, 3);
					State.currentVersion = $"{version[0]}.{version[1]}";
					break;
				} catch (Exception e) {
					Console.WriteLine($"Initial HTTP request failed, retrying - {e.Message}");
					await Task.Delay(3000);
				}
			}
			Console.WriteLine($"Game version {State.currentVersion}");
			Console.WriteLine($"Logged in as summoner {State.summonerId}");
		}

		public static Task<string> Get(string uri) => client.GetStringAsync(uri);
		public static async Task<Response> Delete(string uri) => await Response.FromResponse(await client.DeleteAsync(uri));
		public static Task<Response> PutJson(string uri, string content) => SendJson("PUT", uri, content);
		public static Task<Response> PostJson(string uri, string content) => SendJson("POST", uri, content);
		public static Task<Response> PatchJson(string uri, string content) => SendJson("PATCH", uri, content);

		public static async Task<Json.Node> GetJson(string uri) => Json.Node.Parse(await client.GetStringAsync(uri));
		//public static Task<Json.Node> DeleteJson(string uri) => ParseResponse(client.DeleteAsync(uri));
		//public static Task<Json.Node> PutJson(string uri, string content) => SendJson("PUT", uri, content);
		//public static Task<Json.Node> PostJson(string uri, string content) => SendJson("POST", uri, content);
		//public static Task<Json.Node> PatchJson(string uri, string content) => SendJson("PATCH", uri, content);

		static async Task<Response> SendJson(string method, string uri, string content) => await Response.FromResponse(await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), uri) {
			Content = new StringContent(content, Encoding.UTF8, "application/json")
		}));
		//static Task<Json.Node> SendJson(string method, string uri, string content) => ParseResponse(client.SendAsync(new HttpRequestMessage(new HttpMethod(method), uri) {
		//	Content = new StringContent(content, Encoding.UTF8, "application/json")
		//}));
		//static async Task<Json.Node> ParseResponse(Task<HttpResponseMessage> response) => Json.Node.Parse(await (await response).Content.ReadAsStringAsync());
	}
}