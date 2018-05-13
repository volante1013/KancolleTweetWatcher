using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KancolleTweetWatcher
{
    public static class AzureHttpRequester
    {
		private static HttpClient client = new HttpClient();

		private static readonly string username = ConfigurationManager.AppSettings.Get("DEPLOYUSERNAME");
		private static readonly string password = ConfigurationManager.AppSettings.Get("DEPLOYPASSWORD");

		public static void SetRequestHeaders ()
		{
			client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
			client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", "*");
		}

		public static async Task<TwitterUserData> GetUserData ()
		{
			var response = await client.GetAsync("https://kancolletweetwatcher.scm.azurewebsites.net/api/vfs/site/wwwroot/tweetdata.json", HttpCompletionOption.ResponseContentRead);

			var str = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<TwitterUserData>(str) ?? new TwitterUserData();
		}

		public static async Task<string> PutUserData (TwitterUserData twitterUserData)
		{
			var response = await client.PutAsJsonAsync("https://kancolletweetwatcher.scm.azurewebsites.net/api/vfs/site/wwwroot/tweetdata.json", twitterUserData);
			return $"PutUserData Status => {response.StatusCode}:{response.StatusCode.ToString()}";
		}

		public static async Task<FunctionJson> GetFunctionJson ()
		{
			var response = await client.GetAsync("https://kancolletweetwatcher.scm.azurewebsites.net/api/vfs/site/wwwroot/NotifyFunc/function.json");
			var str = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<FunctionJson>(str);
		}

		public static async Task<string> PutFunctionJson (FunctionJson functionJson)
		{
			var funcJson = JsonConvert.SerializeObject(functionJson);
			var response = await client.PutAsJsonAsync("https://kancolletweetwatcher.scm.azurewebsites.net/api/vfs/site/wwwroot/NotifyFunc/function.json", funcJson);
			return $"PutFuncData Status => {response.StatusCode}:{response.StatusCode.ToString()}";
		}

		public static async Task<string> SyncTrigger ()
		{
			var response = await client.PostAsync("https://kancolletweetwatcher.scm.azurewebsites.net/api/functions/synctriggers", null);
			return $"Sync Status => {response.StatusCode}:{response.StatusCode.ToString()}";
		}
	}
}
