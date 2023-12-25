using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Linq;
using Discord.Rest;

namespace TestDiscordBot
{
	public interface ICog
	{

	}

	public class ExampleCog //: ICog
	{
		private DiscordSocketClient Client;
		public ExampleCog(DiscordSocketClient client, string[] prgargs)
		{
			Client = client;

			Client.MessageReceived += MessageReceived;
		}

		internal async Task MessageReceived(SocketMessage msg) 
		{
			Console.WriteLine(msg);
		}

		public void Dispose() { Console.WriteLine("ExampleCog Disposed"); }
	}
}
