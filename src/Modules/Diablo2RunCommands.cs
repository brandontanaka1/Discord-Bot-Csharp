using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord_Bot_Csharp.src.Data_Access;
using MongoDB.Driver.Linq;
using MongoDB.Driver;
using System.Linq;

namespace Discord_Bot_Csharp.src.Modules
{
    public class Diablo2RunCommands : ModuleBase<SocketCommandContext>
    {
        private string ConnectionString
        {
            get
            {
                return @"mongodb://localhost:27017";
            }
        }

        [Command("tracking")] // Command name.
        [Discord.Commands.Summary("Display tracking button")]
        public async Task StartRuns([Remainder] string message = "")
        {
            var component = new Discord.ComponentBuilder();
            component = component.WithButton("Start Tracking Runs", style: ButtonStyle.Primary, customId: "tracking");

            await ReplyAsync($"Click the button to start tracking runs!", components: component.Build());
        }

        [Command("currentruns")]
        [Discord.Commands.Summary("Display current runs")]
        public async Task DisplayCurrentRuns([Remainder] string message = "")
        {
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runners = await runnerController.GetQuery().Where(runner => runner.CurrentGame != null).ToListAsync();
            var currentGames = runners.Select(runner => runner.CurrentGame.Value).ToList();
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            var games = await gameController.GetQuery().Where(game => currentGames.Contains(game.Id)).ToListAsync();

            var formattedReply = $"Current runs:\n";

            foreach (var runner in runners)
            {
                var currentGame = games.Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(currentGame.GameName))
                {
                    formattedReply += $"User: {runner.Name} - Platform: {currentGame.Platform} - Game Type: {currentGame.GameType} - Run Type: {currentGame.RunType} - Game Name: {currentGame.GameName} - Password: {currentGame.GamePassword}\n\n";
                }
            }
            await ReplyAsync(formattedReply);
        }

        [Command("leaderboards")]
        [Discord.Commands.Summary("Display leaderboards")]
        public async Task DisplayLeaderboards([Remainder] string message = "")
        {
            //var statsController = new BaseDataController<Diablo2RunnerStats>(ConnectionString);
            //var stats = await statsController.GetQuery().GroupBy(stats => stats.RunnerName).(stats => stats.RunCount).FirstOrDefaultAsync();

            await ReplyAsync($"Leaderboards:\nCOMING SOON!");
        }

        [Command("commands")]
        [Discord.Commands.Summary("Display commands")]
        public async Task DisplayCommands([Remainder] string message = "")
        {
            await ReplyAsync($"Supported commands:\n$tracking\n$currentruns\n$leaderboards");
        }
    }
}
