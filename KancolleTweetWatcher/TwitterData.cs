﻿using System.Collections.Generic;
using System.Text;

namespace KancolleTweetWatcher
{
	public class TwitterUserData
	{
		public int LastStatusesCount;
		public List<TweetData> tweetDatas;

		public TwitterUserData ()
		{
			LastStatusesCount = 0;
			tweetDatas = new List<TweetData>();
		}
	}

	public class TweetData
	{
		public string Day;
		public string Time;
		public string Url;
		public string Text;

		public TweetData () : this("1/1", "11:00-18:00", "https://www.google.co.jp/", "これはサンプルツイートです") {	}

		public TweetData (string day, string time, string url, string text)
		{
			this.Day = day;
			this.Time = time;
			this.Url = url;
			this.Text = text;
		}

		public bool IsAnyEmpty ()
		{
			return this.Day == string.Empty || this.Time == string.Empty || this.Url == string.Empty;
		}

		public override string ToString ()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"Day : {this.Day}");
			sb.AppendLine($"Time : {this.Time}");
			sb.AppendLine($"Url : {this.Url}");
			sb.AppendLine($"Text : {this.Text}");

			return sb.ToString();
		}

		public string GetCronStr()
		{
			if (!IsAnyEmpty()) return string.Empty;

			string cronStr = "";
			string[] date = Day.Split('/');

			// TODO: 今は午前7時固定だが必要になったらどの時間でも指定できるようにする
			cronStr += "0 0 7 ";
			cronStr += date[1] + " " + date[0];
			cronStr += " *";

			return cronStr;
		}
	}
}
