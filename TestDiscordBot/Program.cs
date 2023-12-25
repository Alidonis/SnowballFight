using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

#if !NET8_0_OR_GREATER
#error Expected .NET8
#endif



namespace TestDiscordBot
{
	internal class Program
	{
		public static Assembly thisAssembly;
		private static List<ICog> cogs = new();

		private static DiscordSocketClient _Client;
		private static List<string> args = new();
		public async static Task Main(string[] args) => await AsyncMain(args);
		async static Task AsyncMain(string[] _args)
		{
#if DEBUG

			bool settingup = true;

			while (settingup) 
			{
				string? input = Console.ReadLine();

				if (input == null || input == "") 
					settingup = false;
				else
					args.Add(input);
			}
#endif

			Console.WriteLine("Starting...");

			var config = new DiscordSocketConfig()
			{
				GatewayIntents = 
				GatewayIntents.MessageContent
				| GatewayIntents.GuildMembers
				| GatewayIntents.AllUnprivileged
			};

			_Client = await SetupClient(SensitiveBotData.BotTokenRADIANT, config);

			_Client.Ready += onReady;

			Console.ReadKey();

			foreach (ICog cog in cogs)
			{
				Type CogType = cog.GetType();
				MethodInfo? DisposeMethod = CogType.GetMethod("Dispose");
				if (DisposeMethod != null)
				{
					DisposeMethod.Invoke(cog, null);
				}
			}

			Task.WaitAll(_Client.LogoutAsync());
		}
		async static Task<DiscordSocketClient> SetupClient(string Token, DiscordSocketConfig config)
		{
			DiscordSocketClient _cl = new(config);

			await _cl.LoginAsync(TokenType.Bot, Token);

			await _cl.StartAsync();

			return _cl;
		}
		private static async Task InitiateBot(DiscordSocketClient _Client)
		{
			Console.WriteLine($"Logging in as : {_Client.CurrentUser.GlobalName}");

			thisAssembly = Assembly.GetAssembly(typeof(Program));
			Console.WriteLine("Reading from " + thisAssembly.FullName);
			
			Type[] AssemblyTypes = thisAssembly.GetTypes();
			List<Type> CogTypes = new();

			foreach (Type type in AssemblyTypes)
			{
				if (type.IsAssignableTo(typeof(ICog)) && type != typeof(ICog))
					CogTypes.Add(type);
			}
			foreach (Type type in CogTypes)
			{
				Console.WriteLine("Loading cog " + type.FullName);
				var parameters = new object[2];
				parameters[0] = _Client;
				parameters[1] = args.ToArray();
				ICog C = Activator.CreateInstance(type, parameters) as ICog;
				Console.WriteLine("Cog is " + C);
				cogs.Add(C);
			}
		}
		private static async Task onReady()
		{
			await InitiateBot(_Client);
		}
	}
}
