using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CoreTweet;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace KancolleTweetWatcher
{
	public static class TweetMonitor
	{
		private static readonly string consumerKey = Environment.GetEnvironmentVariable("TwitterConsumerKey");
		private static readonly string consumerSecret = Environment.GetEnvironmentVariable("TwitterConsumerSecret");
		private static OAuth2Token apponly = OAuth2.GetToken(consumerKey, consumerSecret);
		private static readonly string ScreenName = "KanColle_STAFF";

		private static readonly string username = Environment.GetEnvironmentVariable("DEPLOYUSERNAME");
		private static readonly string password = Environment.GetEnvironmentVariable("DEPLOYPASSWORD");

		private static readonly string regexDays = @"[0-9]+\/[0-9]+(\(.\))*";
		private static readonly string regexTimes = @"[0-9]+\:[0-9]+";

		private static HttpClient client = new HttpClient();
		private static TelemetryClient ai = new TelemetryClient();

#if DEBUG
		private const string scheduleExpression = "0 53 * * * *";
#else
		private const string scheduleExpression = "0 0 7 * * 1";
#endif

		[FunctionName("TweetMonitor")] // 毎週月曜日の7:00に起動
		public static async Task Run ([TimerTrigger(scheduleExpression)]TimerInfo myTimer, TraceWriter log)
		{
			log.Info($"[{DateTime.Now}] : C# TweetMonitor function processed a request.");

			client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
			client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", "*");

			var count = await apponly.Users.ShowAsync(ScreenName).ContinueWith(s => s.Result.StatusesCount);

			var twiUserData = await GetData();

			var lastStatusesCount = twiUserData?.LastStatusesCount ?? 0;

			twiUserData.LastStatusesCount = count;

			ai.TrackTrace($"OldStatuesCount={lastStatusesCount}, NowCount={count}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);

			var diffCount = Math.Min(Math.Max(0, count - lastStatusesCount), 50);
			if(diffCount == 0)
			{
				log.Info("Nothing New Tweet.");
				return;
			}

			var param = new Dictionary<string, object>()
			{
				["count"] = diffCount,
				["screen_name"] = ScreenName,
			};

			var maintenanceTweets = await apponly.Statuses.UserTimelineAsync(param).ContinueWith(tl => tl.Result.Where(t => t.Text.Contains("メンテ")));
			foreach(var tweet in maintenanceTweets)
			{
				var Days = Regex.Matches(tweet.Text, regexDays).Cast<Match>().Select(match => match.Value);

				if (!Days.Any())
					continue;

				var Times = Regex.Matches(tweet.Text, regexTimes).Cast<Match>().Select(match => match.Value);
				var timeStr = string.Join("-", Times);

				var url = tweet.Entities.Urls.FirstOrDefault()?.Url ?? "";

				var tweetData = new TweetData(Days.FirstOrDefault(), timeStr, url, tweet.Text);
				twiUserData.tweetDatas.Add(tweetData);

				PutData(twiUserData);

				if (!tweetData.IsAnyEmpty())
				{
					NotifyFunc.Run(null, log);
				}
			}
		}

		private static async Task<TwiUserData> GetData ()
		{
			var response = await client.GetAsync("https://kancolletweetwatcher.scm.azurewebsites.net/api/vfs/site/wwwroot/tweetdata.json", HttpCompletionOption.ResponseContentRead);

			var str =  await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<TwiUserData>(str) ?? new TwiUserData();
		}

		private static async void PutData (TwiUserData twiUserData)
		{
			var response = await client.PutAsJsonAsync("https://kancolletweetwatcher.scm.azurewebsites.net/api/vfs/site/wwwroot/tweetdata.json", twiUserData);
			ai.TrackTrace(response.ToString(), Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);
		}
	}
}
