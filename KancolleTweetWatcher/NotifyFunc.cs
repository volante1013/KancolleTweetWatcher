using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using PushbulletSharp;
using PushbulletSharp.Models.Requests;

namespace KancolleTweetWatcher
{
	public static class NotifyFunc
    {
		private static readonly string PBAccessToken = ConfigurationManager.AppSettings.Get("PushBulletAccessToken");
		private static readonly PushbulletClient client = new PushbulletClient(PBAccessToken);

		private static TraceWriter log;

#if DEBUG
		private const string scheduleExpression = "30 0 0 * * *";
#else
		private const string scheduleExpression = "0 0 7 23 4 *";
#endif

		[FunctionName("NotifyFunc")]
		public static void Run ([TimerTrigger(scheduleExpression)]TimerInfo myTimer, TraceWriter writer)
        {
			log = writer;
            log.Info($"[{DateTime.Now}] : C# NotifyFunc function processed a request.");

			Task.Run(() => NotifyPushBullet());
		}

		public static async Task NotifyPushBullet (TwitterUserData userData = null)
		{
			AzureHttpRequester.SetRequestHeaders();
			var twiUserData = (userData == null) ?  await AzureHttpRequester.GetUserData() : userData;
			var data = twiUserData.tweetDatas.FirstOrDefault(tweet => !tweet.IsAnyEmpty()) ?? new TweetData();
			log.Info(data.ToString());

			// PushBulletのクライアントの情報を取得
			var info = client.CurrentUsersInformation();
			if (info == null)
			{
				log.Error("[Error] PushBullet CurrentUsersInfomation is null!");
				return;
			}

			// リンクを付与したリクエストの作成
			var request = new PushLinkRequest()
			{
				Email = info.Email,
				Title = $"[{DateTime.Now}]\n司令官! メンテナンス日時についての報告です!",
				Url = data.Url,
				Body = $"{data.Day}({data.Time})に\n艦これのメンテナンスが行われます!\nご注意ください!\n(以下、大本営発表)\n"
			};

			// リクエストのPushを実行
			log.Info($"PushLink Response:\n{client.PushLink(request).ToJson()}");
		}
	}
}
