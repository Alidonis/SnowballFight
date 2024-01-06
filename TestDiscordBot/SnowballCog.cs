using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.Entity.Infrastructure.MappingViews;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;

namespace TestDiscordBot
{
	public class SnowballCog : ICog
	{
		private DiscordSocketClient Client;
		private DatabaseManager Database;
		private Random RNG;

		/// <summary>
		/// Param 1 : Int64 User ID
		/// Param 2 : Int64 Snowballs reserve
		/// Param 3 : Int64 Snowballs thrown
		/// Param 4 : Int64 Snowballs Received
		/// Param 5 : Int64 Total snowballs aquired
		/// Param 6 : DateTimeOffset LastMessageTimestamp
		/// </summary>
		List<Tuple<long, long, long, long, long, DateTimeOffset>> Cache;
		public SnowballCog(DiscordSocketClient client, string[] prgargs) 
		{
			Client = client;
			Client.MessageReceived += onMessage;
			Database = new("../../../db/snowball.db");

			RNG = new Random();
			Cache = new();

			var collums = new Dictionary<string, string>()
			{
				{ "user_id", "INTEGER PRIMARY KEY" },
				{ "SnowballsReserve", "INTEGER" },
				{ "ThrownSnowballs", "INTEGER" },
				{ "ReceivedSnowballs", "INTEGER" },
				{ "SnowballsTotalAquired", "INTEGER" },
			};

			Database.CreateTable("snowballs", collums, true);

			var lbInfoCollumns = new Dictionary<string, string>()
			{
				{ "server_id", "INTEGER PRIMARY KEY" },
				{ "channel_id", "INTEGER" },
				{ "LeaderBoardMSG", "INTEGER" },
			};

			Database.CreateTable("leaderboardinfo", lbInfoCollumns, true);
		}

		private async Task<Tuple<long?, long?, long?, long?, long?>> GetUserData(SocketUser user)
		{
			(
				long? user_id,
				long? snowball_reserve,
				long? snowballs_thrown,
				long? snowballs_received,
				long? total_snowballs_aquired
			) = (null, null, null, null, null);

			foreach (var item in Cache)
			{
				if (item.Item1 == (long)user.Id)
				{
					(
					user_id,
					snowball_reserve,
					snowballs_thrown,
					snowballs_received,
					total_snowballs_aquired,
					_
					) = item;
				}
			}

			if (user_id == null)
			{
				object[][] records = await Database.SelectFromTableAsync("snowballs", "user_id, SnowballsReserve, ThrownSnowballs, ReceivedSnowballs, SnowballsTotalAquired", $"user_id={user.Id}");

				if (records.Length == 0)
				{
					(
						user_id,
						snowball_reserve,
						snowballs_thrown,
						snowballs_received,
						total_snowballs_aquired
					) = await CreateUserData(user);
				}
				else
				{
					(
					user_id,
					snowball_reserve,
					snowballs_thrown,
					snowballs_received,
					total_snowballs_aquired
					) = ((long)records[0][0], (long)records[0][1], (long)records[0][2], (long)records[0][3], (long)records[0][4]);
				}
			}
			return new(
				user_id,
				snowball_reserve,
				snowballs_thrown,
				snowballs_received,
				total_snowballs_aquired
			);
		}

		private async Task WriteCacheToDatabase()
		{
			foreach (var item in Cache)
			{
				(
					long user_id, 
					long snowball_reserve, 
					long snowballs_thrown, 
					long snowballs_received, 
					long total_snowballs_aquired,
					_
				) = item;

				string[] items = [user_id.ToString(), snowball_reserve.ToString(), snowballs_thrown.ToString(), snowballs_received.ToString(), total_snowballs_aquired.ToString()];
				
				Database.AppendRecord("snowballs", items, true);
			}
			Cache.Clear();
		}

		private async Task WriteCachedItemToDatabase(int index)
		{
			(
				long user_id,
				long snowball_reserve,
				long snowballs_thrown,
				long snowballs_received,
				long total_snowballs_aquired,
				_
			) = Cache[index];

			string[] items = [user_id.ToString(), snowball_reserve.ToString(), snowballs_thrown.ToString(), snowballs_received.ToString(), total_snowballs_aquired.ToString()];

			Database.AppendRecord("snowballs", items, true);
		}

		private async Task CleanupCache()
		{
			DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow;
			foreach (var item in Cache)
			{
				if (dateTimeOffset > item.Item6.AddMinutes(1)) 
				{
					Console.ForegroundColor = ConsoleColor.Blue;
					Console.WriteLine($"Cleared elements from cache : user_id {item.Item1}");
					Console.ForegroundColor = ConsoleColor.White;
					await WriteCachedItemToDatabase(Cache.IndexOf(item));
					Cache.Remove(item);
				}
			}

			if (Cache.Count <= 10)
				return;
				
			int EarliestTime = 0;
			for (int CacheIterator = 0; CacheIterator < Cache.Count; CacheIterator++)
			{
				if (Cache[CacheIterator].Item6 < Cache[EarliestTime].Item6)
					EarliestTime = CacheIterator;
			}
			await WriteCachedItemToDatabase(EarliestTime);
			Cache.RemoveAt(EarliestTime);
		}

		private async Task WriteDataToCache(long user_id, long snowball_reserve, long snowballs_thrown, long snowballs_received, long total_snowballs_aquired, DateTimeOffset Timestamp)
		{
			Tuple<long, long, long, long, long, DateTimeOffset> items = new(user_id, snowball_reserve, snowballs_thrown, snowballs_received, total_snowballs_aquired, Timestamp);

			for ( int CacheIter = 0; CacheIter < Cache.Count; CacheIter++ )
			{
				if (Cache[CacheIter].Item1 == user_id)
				{
					Cache.RemoveAt(CacheIter);
					Cache.Insert(CacheIter, items);
					return;
				}
			}

			Cache.Add(items);
		}

		private async Task<Tuple<long, long, long, long, long>> CreateUserData(SocketUser user)
		{
			long user_id = (long)user.Id;
			long snowball_reserve = 10;
			long snowballs_thrown = 0;
			long snowballs_received = 0;
			long total_snowballs_aquired = 0;

			return new (user_id, snowball_reserve, snowballs_thrown, snowballs_received, total_snowballs_aquired);
		}

		private async Task GiveSnowballOnMessage(SocketMessage message, bool debug_forcesnowball = false)
		{
			(
				long? user_id,
				long? snowball_reserve,
				long? snowballs_thrown,
				long? snowballs_received,
				long? total_snowballs_aquired
			) = GetUserData(message.Author).Result;

			DateTimeOffset offset = DateTimeOffset.UtcNow;
			var timeOfDay = (offset.Hour * 60 + offset.Minute);

			double snowballsMult = Math.Sin((timeOfDay / 1440) * Math.PI);

			int AccuracyMultiplier = 100;
			if (RNG.Next(25) * AccuracyMultiplier <= Math.Floor(2 * (snowballsMult * AccuracyMultiplier)) || debug_forcesnowball) 
			{
				snowball_reserve += 1;
				total_snowballs_aquired += 1;
				await Discord.MessageExtensions.ReplyAsync((IUserMessage)message, $"You got a snowball ! (total {snowball_reserve})");
			}

			await WriteDataToCache((long)user_id, (long)snowball_reserve, (long)snowballs_thrown, (long)snowballs_received, (long)total_snowballs_aquired, message.Timestamp);

			await CleanupCache();
		}
		private async Task UpdateLeaderBoard(SocketGuild guild)
		{
			object[][] info = await Database.SelectFromTableAsync("leaderboardinfo", "(server_id, channel_id, LeaderBoardMSG)", $"server_id={guild.Id}");

			object[]? leaderboardInfo = null;

			foreach (object[] row in info)
			{
				if ((long)row[0] == (long)guild.Id)
				{
					leaderboardInfo = row;
					break;
				}
			}

			if (leaderboardInfo == null)
			{
				return;
			}

			(long serverID, long channelID, long messageID) = ((long)leaderboardInfo[0], (long)leaderboardInfo[1], (long)leaderboardInfo[2]);


			object[][] ActiveThrowersTable = Database.SelectFromTable("snowballs", "user_id, SnowballsReserve", order: new Dictionary<string, string>() { { "SnowballsReserve", "DESC" } }, limit: 10);
			object[][] BestPeltersTable = Database.SelectFromTable("snowballs", "user_id, ThrownSnowballs", order: new Dictionary<string, string>() { { "ThrownSnowballs", "DESC" } }, limit: 10);
			object[][] MostPeltedTable = Database.SelectFromTable("snowballs", "user_id, ReceivedSnowballs", order: new Dictionary<string, string>() { { "ReceivedSnowballs", "DESC" } }, limit: 10);

			string LbMostActiveThrowers = "";
			string LbBestPelters = "";
			string LbMostPelted = "";

			foreach (object[] objects in ActiveThrowersTable)
			{
				LbMostActiveThrowers += $" {guild.GetUser((ulong)objects[0]).Username}";
			}

			EmbedBuilder leaderboardsBuilder = new EmbedBuilder();

			leaderboardsBuilder.AddField("Most snowball thrown", LbMostActiveThrowers);
			leaderboardsBuilder.AddField("Most ", LbBestPelters);
			leaderboardsBuilder.AddField("Most snowball thrown", LbMostPelted);

			ISocketMessageChannel channel = guild.GetChannel((ulong)channelID) as ISocketMessageChannel;
			RestUserMessage msg = channel.GetMessageAsync((ulong)messageID).Result as RestUserMessage;

			await msg.ModifyAsync(msg => { 
				msg.Content = "";
				msg.Embed = leaderboardsBuilder.Build();
			});
		}

		private async Task SetLeaderboardChannel(SocketMessage Context)
		{
			RestUserMessage Message = await Context.Channel.SendMessageAsync("Hang on! Generating leaderboard...");
			SocketGuildChannel channel = Message.Channel as SocketGuildChannel;

			Database.AppendRecord("leaderboardinfo", [channel.Guild.Id.ToString(), Message.Channel.Id.ToString(), Message.Id.ToString() ], true);
			await UpdateLeaderBoard(channel.Guild);
		}
		private async Task throwsnowball(SocketMessage userContext, SocketUser pelted)
		{
			(
				long? user_id,
				long? snowball_reserve,
				long? snowballs_thrown,
				long? snowballs_received,
				long? total_snowballs_aquired
			) = GetUserData(userContext.Author).Result;

			(
				long? pelted_user_id,
				long? pelted_snowball_reserve,
				long? pelted_snowballs_thrown,
				long? pelted_snowballs_received,
				long? pelted_total_snowballs_aquired
			) = GetUserData(pelted).Result;

			if (snowball_reserve < 1)
			{
				userContext.Channel.SendMessageAsync("Sorry, comrade, no snowballs to your name.");
				return;
			}

			snowball_reserve -= 1;

			string gifName = $"SnowballPelt{RNG.Next(1, Directory.GetFiles("./../../../SnowballGIFs").Length-1)}.gif";

			EmbedBuilder snowballsEmbedBuilder = new
				EmbedBuilder()
				.AddField(". . Womp womp :00_santahat:", $"{userContext.Author.Mention} just pelted {pelted.Mention} with a snowball. Ohoh, it's on!", true)
				.WithThumbnailUrl($"attachment://{gifName}")
				.WithFooter($"{userContext.Author.Mention}, you have {snowball_reserve} snowballs left.  ୨୧  Run /cmd to check your stats!")
				.WithColor((Color)Color.MaxDecimalValue);

			Embed snowballsEmbed = snowballsEmbedBuilder.Build();

			await userContext.Channel.SendFileAsync($"./../../../SnowballGIFs/{gifName}", embeds: [snowballsEmbed]);

			await WriteDataToCache((long)user_id, (long)snowball_reserve, (long)snowballs_thrown, (long)snowballs_received, (long)total_snowballs_aquired, userContext.Timestamp);
			await WriteDataToCache((long)pelted_user_id, (long)pelted_snowball_reserve, (long)pelted_snowballs_thrown, (long)pelted_snowballs_received, (long)pelted_total_snowballs_aquired, userContext.Timestamp);
		}

		private async Task SetSnowballsToZero(SocketMessage userContext)
		{
			(
				long? user_id,
				long? snowball_reserve,
				long? snowballs_thrown,
				long? snowballs_received,
				long? total_snowballs_aquired
			) = GetUserData(userContext.Author).Result;

			SocketUser user = userContext.MentionedUsers.Count == 0 ? userContext.Author : userContext.MentionedUsers.First();

			snowball_reserve = 0;

			await userContext.Channel.SendMessageAsync($"Set snowballs to 0 for {userContext.Author.GlobalName}.");

			await WriteDataToCache((long)user_id, (long)snowball_reserve, (long)snowballs_thrown, (long)snowballs_received, (long)total_snowballs_aquired, userContext.Timestamp);
		}

		public async Task onMessage(SocketMessage message)
		{
			if (message.Author.IsBot)
				return;

			switch (message.ToString().Split(' ')[0])
			{
				case ".throw":
					if (message.MentionedUsers.Count == 0)
					{
						await message.Channel.SendMessageAsync("You must mention a user to throw at !");
						return;
					}
					await throwsnowball(message, message.MentionedUsers.First());
					break;
				case ".setlbchannel":
					await SetLeaderboardChannel(message);
					break;
				case ".forcesnowball":
					await GiveSnowballOnMessage(message, true);
					break;
				case ".setsnowballstozero":
					await SetSnowballsToZero(message);
					break;
				case ".forcecachecleanup":
					await CleanupCache();
					break;
				case ".writecachetodatabase":
					await WriteCacheToDatabase();
					break;
				default:
					await GiveSnowballOnMessage(message);
					break;
			}

			SocketGuildChannel ch = message.Channel as SocketGuildChannel;

			UpdateLeaderBoard(ch.Guild);
		}

		public void Dispose() 
		{
			Task.WaitAll(WriteCacheToDatabase());
			Database.Dispose();
		}
	}
}
