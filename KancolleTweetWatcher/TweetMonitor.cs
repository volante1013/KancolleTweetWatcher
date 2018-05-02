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

			// HttpClientのHeaderの設定
			AzureHttpRequester.SetRequestHeaders();

			// 艦これ公式のツイート数を取得
			var count = await apponly.Users.ShowAsync(ScreenName).ContinueWith(s => s.Result.StatusesCount);

			// 前回起動時のユーザーデータを取得
			var twiUserData = await AzureHttpRequester.GetUserData();
			twiUserData.tweetDatas.Clear();

			// 前回のツイート数を取得
			var lastStatusesCount = twiUserData?.LastStatusesCount ?? 0;

			// 前回からどれだけツイートしたかを算出、ツイートしていなければreturn
			var diffCount = Math.Min(Math.Max(0, count - lastStatusesCount), 50);
			if(diffCount == 0)
			{
				log.Info("Nothing New Tweet.");
				return;
			}
			// ツイート数の更新
			twiUserData.LastStatusesCount = count;

			log.Info($"OldStatuesCount={lastStatusesCount}, NowCount={count}");

			// メンテという文字が含まれたツイートがあるかどうかを前回からのツイート数分だけ検索・取得
			var param = new Dictionary<string, object>() { ["count"] = diffCount, ["screen_name"] = ScreenName };
			var maintenanceTweets = await apponly.Statuses.UserTimelineAsync(param).ContinueWith(tl => tl.Result.Where(t => t.Text.Contains("メンテ")));
			if (maintenanceTweets.Any())
			{
				// メンテツイートが一つでもあれば
				foreach (var tweet in maintenanceTweets)
				{
					// 正規表現で00/00となる部分を抽出
					var Days = Regex.Matches(tweet.Text, regexDays).Cast<Match>().Select(match => match.Value);

					// ツイートに日付が一つもなければスキップ
					if (!Days.Any()) continue;

					// 正規表現で00:00となる部分を抽出
					var Times = Regex.Matches(tweet.Text, regexTimes).Cast<Match>().Select(match => match.Value);
					// すべての時間をハイフンでつなぐ
					// TODO: 時間が3つ以上あったときの処理
					var timeStr = string.Join("-", Times);

					// ツイートのURLの取得
					// TODO: ツイートのURLが取得できないことがある原因の調査
					var url = tweet.Entities.Urls.FirstOrDefault()?.Url ?? "";

					// ツイートデータを作成して、ユーザーデータに追加
					var tweetData = new TweetData(Days.FirstOrDefault(), timeStr, url, tweet.Text);
					twiUserData.tweetDatas.Add(tweetData);

					// ツイートに日付と時間,URLが含まれていたら
					if (!tweetData.IsAnyEmpty())
					{
						// PushBulletで通知
						await NotifyFunc.NotifyPushBullet(twiUserData);
					}
				}
			}
			else
			{
				// 一つもなければ
				log.Info("Nothing tweets what the text contains \"メンテ\". ");
			}

			// ユーザーデータの更新を反映
			log.Info(await AzureHttpRequester.PutUserData(twiUserData));
		}
	}
}
