using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_Bot_Csharp.src.Data_Access;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Discord_Bot
{
    public class Diablo2RunInteractions : InteractionModuleBase<SocketInteractionContext>
    {
        private string ConnectionString
        {
            get
            {
                return @"mongodb://localhost:27017";
            }
        }

        // Defines the modal that will be sent.
        public class GameInfoModal : IModal
        {
            public string Title => "Game Information";

            // Strings with the ModalTextInput attribute will automatically become components.
            [InputLabel("Game Name")]
            [ModalTextInput("game_name", placeholder: "run1")]
            public string Name { get; set; }

            // Additional paremeters can be specified to further customize the input.
            [InputLabel("Game Password")]
            [ModalTextInput("game_password", placeholder: "leave blank if this is a public game", minLength: 0, initValue: " ")]
            public string Password { get; set; }
        }

        // Responds to the modal.
        [ModalInteraction("game_info")]
        public async Task ModalResponse(GameInfoModal modal)
        {
            // Build the message to send.
            string message = $"Game Name: {modal.Name}\nGame Password: {modal.Password}";

            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            var game = gameController.GetQuery().Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();
            game.GameName = modal.Name;
            game.GamePassword = modal.Password.Trim();

            var filter = Builders<Diablo2Game>.Filter.Eq(game => game.Id, runner.CurrentGame.Value);
            await gameController.GetCollection().ReplaceOneAsync(filter, game);

            // Specify the AllowedMentions so we don't actually ping everyone.
            AllowedMentions mentions = new AllowedMentions()
            {
                AllowedTypes = AllowedMentionTypes.Users
            };

            var component = new Discord.ComponentBuilder();
            component = component.WithButton("Next Run", style: ButtonStyle.Success, customId: "runinteraction:next");
            component = component.WithButton("Stop Tracking", style: ButtonStyle.Primary, customId: "runinteraction:stop");

            // Respond to the modal.
            await RespondAsync(message, allowedMentions: mentions, ephemeral: true, components: component.Build());
            await NotifyChannelOfNewRun(runner, game);
        }

        [ComponentInteraction("platform")]
        public async Task HandlePlatformSelected(string platformCode)
        {
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();

            if (runner.CurrentGame != null)
            {
                await StopRunning(runner);
            }

            var component = new Discord.ComponentBuilder();

            var menuOptionsBuilder = new List<SelectMenuOptionBuilder>();
            var list = new List<string>()
            {
                "Americas",
                "Europe",
                "Asia"
            };

            foreach (string value in list)
            {
                var selectMenuOptionBuilder = new SelectMenuOptionBuilder(value, value);
                menuOptionsBuilder.Add(selectMenuOptionBuilder);
            }

            var selectMenuBuilder = new SelectMenuBuilder("regionselected", menuOptionsBuilder);
            component = component.WithSelectMenu(selectMenuBuilder);

            var newGame = new Diablo2Game() { Platform = GetPlatform(platformCode), GameType = GetGameType(platformCode) };
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            await gameController.GetCollection().InsertOneAsync(newGame);

            runner.CurrentGame = newGame.Id;
            var filter = Builders<Diablo2Runner>.Filter.Eq(runner => runner.Name, Context.User.Username);
            await runnerController.GetCollection().ReplaceOneAsync(filter, runner);

            AllowedMentions mentions = new AllowedMentions()
            {
                AllowedTypes = AllowedMentionTypes.Users
            };

            await Context.Interaction.RespondAsync($"Please select your region by making a selection below.", components: component.Build(), allowedMentions: mentions, ephemeral: true);
        }

        [ComponentInteraction("regionselected")]
        public async Task HandleRegionSelected(string region)
        {
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            var game = gameController.GetQuery().Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();
            game.Region = region;
            var filter = Builders<Diablo2Game>.Filter.Eq(game => game.Id, runner.CurrentGame.Value);
            await gameController.GetCollection().ReplaceOneAsync(filter, game);

            var component = new Discord.ComponentBuilder();

            var menuOptionsBuilder = new List<SelectMenuOptionBuilder>();
            var runTypeController = new BaseDataController<RunType>(ConnectionString);
            var runTypes = await runTypeController.GetQuery().ToListAsync();

            foreach (var runType in runTypes)
            {
                var selectMenuOptionBuilder = new SelectMenuOptionBuilder(runType.Name, runType.Value);
                menuOptionsBuilder.Add(selectMenuOptionBuilder);
            }

            var selectMenuBuilder = new SelectMenuBuilder("runtypeselected", menuOptionsBuilder);
            component = component.WithSelectMenu(selectMenuBuilder);

            AllowedMentions mentions = new AllowedMentions()
            {
                AllowedTypes = AllowedMentionTypes.Users
            };

            await Context.Interaction.RespondAsync($"Please select the run type by making a selection below.", components: component.Build(), allowedMentions: mentions, ephemeral: true);
        }

        [ComponentInteraction("runtypeselected")]
        public async Task HandleRunTypeSelected(string runtype)
        {
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            var game = gameController.GetQuery().Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();
            game.RunType = runtype;
            var filter = Builders<Diablo2Game>.Filter.Eq(game => game.Id, runner.CurrentGame.Value);
            await gameController.GetCollection().ReplaceOneAsync(filter, game);

            await Context.Interaction.RespondWithModalAsync<GameInfoModal>("game_info");
        }

        private async Task NotifyChannelOfNewRun(Diablo2Runner runner, Diablo2Game game)
        {
            string message = $"**{runner.Name}** has started a new run!\n**Platform:** {game.Platform}\n**Region:** {game.Region}\n**Game Type:** {game.GameType}\n**Game Name:** {game.GameName}\n**Game Password:** {game.GamePassword}";
            var runTypeController = new BaseDataController<RunType>(ConnectionString);
            var runType = await runTypeController.GetQuery().Where(run => run.Value == game.RunType).FirstOrDefaultAsync();

            if (runType.Channels != null)
            {
                foreach (var channel in runType.Channels)
                {
                    var guild = Context.Client.GetGuild(channel.Guild);
                    await guild.GetTextChannel(channel.Channel).SendMessageAsync(message);
                }          
            }           
        }

        [ComponentInteraction("runinteraction:*")]
        public async Task HandleRunInteraction(string interaction)
        {
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();

            if (string.Equals(interaction, "next", StringComparison.OrdinalIgnoreCase))
            {
                var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
                var game = gameController.GetQuery().Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();
                var updateStatsTask = UpdateStats(game);
                var runName = Regex.Replace(game.GameName, "[0-9]", "");

                game.GameName = $"{runName}{GetNextRunNumber(game.GameName)}";

                var filter = Builders<Diablo2Game>.Filter.Eq(game => game.Id, runner.CurrentGame.Value);
                await gameController.GetCollection().ReplaceOneAsync(filter, game);

                // Specify the AllowedMentions so we don't actually ping everyone.
                AllowedMentions mentions = new AllowedMentions()
                {
                    AllowedTypes = AllowedMentionTypes.Users
                };

                var component = new Discord.ComponentBuilder();
                component = component.WithButton("Next Run", style: ButtonStyle.Success, customId: "runinteraction:next");
                component = component.WithButton("Stop Tracking", style: ButtonStyle.Primary, customId: "runinteraction:stop");
                string message = $"Type: {game.RunType}\nGame Name: {game.GameName}\nGame Password: {game.GamePassword}";

                await updateStatsTask;

                // Respond to the modal.
                await RespondAsync(message, allowedMentions: mentions, ephemeral: true, components: component.Build());
                await NotifyChannelOfNewRun(runner, game);
            }
            else
            {
                // run count is increment in StopRunning
                await StopRunning(runner);
                await Context.Interaction.RespondAsync($"Run tracking session has ended. Thank you!", ephemeral: true);
            }
        }

        private string GetNextRunNumber(string input)
        {
            var number = Regex.Replace(input, "[^0-9]", "");
            var runNumber = string.IsNullOrWhiteSpace(number) ? 0 : Convert.ToInt32(number);
            var count = number.TakeWhile(c => c == '0').Count();
            runNumber = runNumber + 1;

            return runNumber.ToString("D" + count);
        }

        private async Task StopRunning(Diablo2Runner runner)
        {
            if (!runner.CurrentGame.HasValue)
            {
                return;
            }

            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var game = gameController.GetQuery().Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();
            await UpdateStats(game);
            var gameFilter = Builders<Diablo2Game>.Filter.Eq(game => game.Id, runner.CurrentGame.Value);
            var deleteTask = gameController.GetCollection().DeleteOneAsync(gameFilter);
            runner.CurrentGame = null;
            var filter = Builders<Diablo2Runner>.Filter.Eq(runner => runner.Name, Context.User.Username);
            await runnerController.GetCollection().ReplaceOneAsync(filter, runner);
            await deleteTask;
        }

        private async Task UpdateStats(Diablo2Game game)
        {
            var statsController = new BaseDataController<Diablo2RunnerStats>(ConnectionString);
            var stats = await statsController.GetQuery().Where(stats => stats.RunnerName == Context.User.Username 
                                                                         && stats.Platform == game.Platform
                                                                         && stats.GameType == game.GameType
                                                                         && stats.RunType == game.RunType
                                                                         && stats.Region == game.Region).FirstOrDefaultAsync();

            if (stats == null)
            {
                stats = new Diablo2RunnerStats() 
                { 
                    RunnerName = Context.User.Username,
                    Platform = game.Platform,
                    GameType = game.GameType,
                    RunType = game.RunType,
                    Region = game.Region,
                    RunCount = 1
                };

                await statsController.GetCollection().InsertOneAsync(stats);
            }
            else
            {
                stats.RunCount += 1;
                var filter = Builders<Diablo2RunnerStats>.Filter.Eq(st => st.Id, stats.Id);
                await statsController.GetCollection().ReplaceOneAsync(filter, stats);
            }
        }

        [ComponentInteraction("tracking")]
        public async Task StartTracking()
        {
            var component = new Discord.ComponentBuilder();

            var menuOptionsBuilder = new List<SelectMenuOptionBuilder>();
            var list = new List<string>()
            {
                "HCBNET",
                "HCLBNET",
                "SBNET",
                "SLBNET",
                "HCNS",
                "HCLNS",
                "SNS",
                "SLNS",
                "HCPS",
                "HCLPS",
                "SPS",
                "SLPS",
                "HCXBX",
                "HCLXBX",
                "SXBX",
                "SLXBX"
            };

            foreach (string value in list)
            {
                var selectMenuOptionBuilder = new SelectMenuOptionBuilder($"{GetGameType(value)} {GetPlatform(value)}", value);
                menuOptionsBuilder.Add(selectMenuOptionBuilder);
            }

            var selectMenuBuilder = new SelectMenuBuilder("platform", menuOptionsBuilder);
            component = component.WithSelectMenu(selectMenuBuilder);

            var databaseController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await databaseController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();

            if (runner == null)
            {
                runner = new Diablo2Runner() { Name = Context.User.Username };
                await databaseController.GetCollection().InsertOneAsync(runner);
            }

            if (runner.CurrentGame != null)
            {
                await StopRunning(runner);
            }

            AllowedMentions mentions = new AllowedMentions()
            {
                AllowedTypes = AllowedMentionTypes.Users,
                MentionRepliedUser = true
            };

            await Context.Interaction.RespondAsync($"Starting to track runs for **{Context.User.Username}**! Please select your platform by making a selection below.", components: component.Build(), allowedMentions: mentions, ephemeral: true);
        }

        private string GetGameType(string platformCode)
        {
            if (platformCode.StartsWith("HCL"))
            {
                return "Hardcore (Ladder)";
            }
            else if (platformCode.StartsWith("HC"))
            {
                return "Hardcore (Non-Ladder)";
            }
            else if (platformCode.StartsWith("SL"))
            {
                return "Standard (Ladder)";
            }
            else if (platformCode.StartsWith("S"))
            {
                return "Standard (Non-Ladder)";
            }

            return "unknown";
        }

        private string GetPlatform(string platformCode)
        {
            if (platformCode.EndsWith("BNET"))
            {
                return "PC";
            }
            else if (platformCode.EndsWith("XBX"))
            {
                return "Xbox";
            }
            else if (platformCode.EndsWith("PS"))
            {
                return "Playstation";
            }
            else if (platformCode.EndsWith("NS"))
            {
                return "Nintendo Switch";
            }

            return "unknown";
        }
    }
}
