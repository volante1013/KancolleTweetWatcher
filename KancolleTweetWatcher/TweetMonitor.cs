using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CoreTweet;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace KancolleTweetWatcher
{
	public static class TweetMonitor
	{
		private static readonly string consumerKey = ConfigurationManager.AppSettings.Get("TwitterConsumerKey");
		private static readonly string consumerSecret = ConfigurationManager.AppSettings.Get("TwitterConsumerSecret");
		private static OAuth2Token apponly = OAuth2.GetToken(consumerKey, consumerSecret);
		private static readonly string ScreenName = "KanColle_STAFF";

		private static readonly string regexDays = @"[0-9]+\/[0-9]+(\(.\))*";
		private static readonly string regexTimes = @"[0-9]+\:[0-9]+";

		private static TraceWriter log;

#if DEBUG
		private const string scheduleExpression = "30 0 0 * * *";
#else
		// 毎週月曜日の7:00に起動
		private const string scheduleExpression = "0 0 7 * * 1";
#endif

		[FunctionName("TweetMonitor")]
		public static async Task Run ([TimerTrigger(scheduleExpression)]TimerInfo myTimer, TraceWriter writer)
		{
			log = writer;
			log.Info($"[{DateTime.Now}] : C# TweetMonitor function processed a request.");

			AzureHttpRequester.SetRequestHeaders();

			var count = await apponly.Users.ShowAsync(ScreenName).ContinueWith(s => s.Result.StatusesCount);

			var twiUserData = await AzureHttpRequester.GetUserData();
			twiUserData.tweetDatas.Clear();

			var lastStatusesCount = twiUserData?.LastStatusesCount ?? 0;

			twiUserData.LastStatusesCount = count;

			log.Info($"OldStatuesCount={lastStatusesCount}, NowCount={count}");

			var diffCount = Math.Min(Math.Max(0, count - lastStatusesCount), 50);
			if(diffCount == 0)
			{
				log.Info("Nothing New Tweet.");
				return;
			}

			var param = new Dictionary<string, object>() { ["count"] = diffCount, ["screen_name"] = ScreenName };
			var maintenanceTweets = await apponly.Statuses.UserTimelineAsync(param).ContinueWith(tl => tl.Result.Where(t => t.Text.Contains("メンテ")));
			if (maintenanceTweets.Any())
			{
				foreach (var tweet in maintenanceTweets)
				{
					var Days = Regex.Matches(tweet.Text, regexDays).Cast<Match>().Select(match => match.Value);

					if (!Days.Any())
						continue;

					var Times = Regex.Matches(tweet.Text, regexTimes).Cast<Match>().Select(match => match.Value);
					var timeStr = string.Join("-", Times);

					var url = tweet.Entities.Urls.FirstOrDefault()?.Url ?? "";

					var tweetData = new TweetData(Days.FirstOrDefault(), timeStr, url, tweet.Text);
					twiUserData.tweetDatas.Add(tweetData);

					if (!tweetData.IsAnyEmpty())
					{
						await NotifyFunc.NotifyPushBullet(twiUserData);
					}
				}
			}
			else
			{
				log.Info("Nothing tweets what the text contains \"メンテ\". ");
			}

			log.Info(await AzureHttpRequester.PutUserData(twiUserData));
		}
	}
}
