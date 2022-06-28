using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Discord.Interactions;

namespace Discord_Bot
{
    public class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            using var services = ConfigureServices();

            Console.WriteLine("Ready for takeoff...");
            var client = services.GetRequiredService<DiscordSocketClient>();

            client.Log += Log;
            services.GetRequiredService<CommandService>().Log += Log;
            services.GetRequiredService<InteractionService>().Log += Log;

            // Get the bot token from the Config.json file.
            JObject config = Functions.GetConfig();
            string token = config["token"].Value<string>();

            // Log in to Discord and start the bot.
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
            await services.GetRequiredService<InteractionHandlingService>().InitializeAsync();

            // Run the bot forever.
            await Task.Delay(-1);
        }

        public ServiceProvider ConfigureServices()
        {
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 500,
                LogLevel = LogSeverity.Info
            });

            return new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(new CommandService(new CommandServiceConfig
                { 
                    LogLevel = LogSeverity.Info,
                    DefaultRunMode = Discord.Commands.RunMode.Async,
                    CaseSensitiveCommands = false 
                }))
                .AddSingleton(new InteractionService(client, new InteractionServiceConfig
                {
                    LogLevel = LogSeverity.Info,
                    DefaultRunMode = Discord.Interactions.RunMode.Async
                }))
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<InteractionHandlingService>()
                .BuildServiceProvider();
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}