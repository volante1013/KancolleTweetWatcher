using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

using Newtonsoft.Json;
using PushbulletSharp;
using PushbulletSharp.Models.Requests;

namespace KancolleTweetWatcher
{
	public static class NotifyFunc
    {
		private static readonly string PBAccessToken = Environment.GetEnvironmentVariable("PushBulletAccessToken", EnvironmentVariableTarget.Process);

		[FunctionName("NotifyFunc")]
		public static void/*async Task<object>*/ Run ([TimerTrigger("0 0 7 23 4 *")]TimerInfo myTimer, TraceWriter log
			//[HttpTrigger(AuthorizationLevel.Admin, Route = null, WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log
			)
        {
			var ai = new TelemetryClient();
            log.Info($"[{DateTime.Now}] : C# NotifyFunc function processed a request.");

			string jsonContent = ConfigurationManager.AppSettings.Get("tweetdata");
			var data = JsonConvert.DeserializeObject<TweetData>(jsonContent) ?? new TweetData();
			ai.TrackTrace(jsonContent, Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);

			var client = new PushbulletClient(PBAccessToken);
			var info = client.CurrentUsersInformation();
			if(info == null)
			{
				log.Error("[Error] PushBullet CurrentUsersInfomation is null!");
				return;
			}

			var request = new PushLinkRequest()
			{
				Email = info.Email,
				Title = $"[{DateTime.Now}]\n司令官! メンテナンス日時についての報告です!",
				Url = data.Url,
				Body = $"{data.Day}({data.Time})に\n艦これのメンテナンスが行われます!\nご注意ください!\n(以下、大本営発表)\n"
			};

			ai.TrackTrace(client.PushLink(request).ToJson(), Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
		}
	}
}
