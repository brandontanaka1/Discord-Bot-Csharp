using Discord;
using Discord.Interactions;
using Discord_Bot_Csharp.src.Data_Access;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
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

            [InputLabel("Run Type")]
            [ModalTextInput("run_type", placeholder: "baal or chaos (contact me to add other types here)")]
            public string RunType { get; set; }

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
            string message = $"Type: {modal.RunType}\nGame Name: {modal.Name}\nGame Password: {modal.Password}";

            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            var game = gameController.GetQuery().Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();
            game.RunType = modal.RunType;
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
        }

        [ComponentInteraction("platform:*")]
        public async Task HandlePlatformSelected(string platformCode)
        {
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();

            if (runner.CurrentGame != null)
            {
                await Context.Interaction.RespondAsync($"Already tracking runs. If this is a new session, please end your previous session by clicking the 'Stop Tracking' button.", ephemeral: true);
                return;
            }

            var newGame = new Diablo2Game() { Platform = GetPlatform(platformCode), GameType = GetGameType(platformCode) };
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            await gameController.GetCollection().InsertOneAsync(newGame);
            runner.CurrentGame = newGame.Id;
            var filter = Builders<Diablo2Runner>.Filter.Eq(runner => runner.Name, Context.User.Username);
            await runnerController.GetCollection().ReplaceOneAsync(filter, runner);

            await Context.Interaction.RespondWithModalAsync<GameInfoModal>("game_info");
        }

        [ComponentInteraction("runinteraction:*")]
        public async Task HandleRunInteraction(string interaction)
        {
            // need to update stats here
            var runnerController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await runnerController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();
            var gameController = new BaseDataController<Diablo2Game>(ConnectionString);
            var game = gameController.GetQuery().Where(game => game.Id == runner.CurrentGame.Value).FirstOrDefault();
            var updateStatsTask = UpdateStats(game);

            if (string.Equals(interaction, "next", StringComparison.OrdinalIgnoreCase))
            {               
                var runNumber = Convert.ToInt32(Regex.Replace(game.GameName, "[^0-9]", ""));
                var runName = Regex.Replace(game.GameName, "[0-9]", "");

                game.GameName = $"{runName}{runNumber + 1}";

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

                // Respond to the modal.
                await RespondAsync(message, allowedMentions: mentions, ephemeral: true, components: component.Build());
            }
            else
            {
                var gameFilter = Builders<Diablo2Game>.Filter.Eq(game => game.Id, runner.CurrentGame.Value);
                var deleteTask = gameController.GetCollection().DeleteOneAsync(gameFilter);
                runner.CurrentGame = null;
                var filter = Builders<Diablo2Runner>.Filter.Eq(runner => runner.Name, Context.User.Username);
                await runnerController.GetCollection().ReplaceOneAsync(filter, runner);
                await deleteTask;

                await Context.Interaction.RespondAsync($"Run tracking session has ended. Thank you!", ephemeral: true);
            }

            await updateStatsTask;
        }

        private async Task UpdateStats(Diablo2Game game)
        {
            var statsController = new BaseDataController<Diablo2RunnerStats>(ConnectionString);
            var stats = await statsController.GetQuery().Where(stats => stats.RunnerName == Context.User.Username 
                                                                         && stats.Platform == game.Platform
                                                                         && stats.GameType == game.GameType
                                                                         && stats.RunType == game.RunType).FirstOrDefaultAsync();

            if (stats == null)
            {
                stats = new Diablo2RunnerStats() 
                { 
                    RunnerName = Context.User.Username,
                    Platform = game.Platform,
                    GameType = game.GameType,
                    RunType = game.RunType,
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
            component = component.WithButton("Hardcore (Non-Ladder) Nintendo Switch", style: ButtonStyle.Danger, customId: "platform:HCNS");
            component = component.WithButton("Hardcore (Ladder) Nintendo Switch", style: ButtonStyle.Danger, customId: "platform:HCLNS");
            component = component.WithButton("Standard (Non-Ladder) Nintendo Switch", style: ButtonStyle.Danger, customId: "platform:SNS");
            component = component.WithButton("Standard (Ladder) Nintendo Switch", style: ButtonStyle.Danger, customId: "platform:SLNS");

            component = component.WithButton("Hardcore (Non-Ladder) Playstation", style: ButtonStyle.Primary, customId: "platform:HCPS");
            component = component.WithButton("Hardcore (Ladder) Playstation", style: ButtonStyle.Primary, customId: "platform:HCLPS");
            component = component.WithButton("Standard (Non-Ladder) Playstation", style: ButtonStyle.Primary, customId: "platform:SPS");
            component = component.WithButton("Standard (Ladder) Playstation", style: ButtonStyle.Primary, customId: "platform:SLPS");

            component = component.WithButton("Hardcore (Non-Ladder) Xbox", style: ButtonStyle.Success, customId: "platform:HCXBX");
            component = component.WithButton("Hardcore (Ladder) Xbox", style: ButtonStyle.Success, customId: "platform:HCLXBX");
            component = component.WithButton("Standard (Non-Ladder) Xbox", style: ButtonStyle.Success, customId: "platform:SXBX");
            component = component.WithButton("Standard (Ladder) Xbox", style: ButtonStyle.Success, customId: "platform:SLXBX");

            component = component.WithButton("Hardcore (Non-Ladder) Battle.net", style: ButtonStyle.Secondary, customId: "platform:HCBNET");
            component = component.WithButton("Hardcore (Ladder) Battle.net", style: ButtonStyle.Secondary, customId: "platform:HCLBNET");
            component = component.WithButton("Standard (Non-Ladder) Battle.net", style: ButtonStyle.Secondary, customId: "platform:SBNET");
            component = component.WithButton("Standard (Ladder) Battle.net", style: ButtonStyle.Secondary, customId: "platform:SLBNET");

            var databaseController = new BaseDataController<Diablo2Runner>(ConnectionString);
            var runner = await databaseController.GetQuery().Where(runner => runner.Name == Context.User.Username).FirstOrDefaultAsync();

            if (runner == null)
            {
                runner = new Diablo2Runner() { Name = Context.User.Username };
                await databaseController.GetCollection().InsertOneAsync(runner);
            }

            if (runner.CurrentGame != null)
            {
                await Context.Interaction.RespondAsync($"Already tracking runs. If this is a new session, please end your previous session by clicking the 'Stop Tracking' button.", ephemeral: true);
                return;
            }

            AllowedMentions mentions = new AllowedMentions()
            {
                AllowedTypes = AllowedMentionTypes.Users,
                MentionRepliedUser = true
            };

            await Context.Interaction.RespondAsync($"Starting to track runs for **{Context.User.Username}**! Please select your platform by clicking a button below.", components: component.Build(), allowedMentions: mentions, ephemeral: true);
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
                return "Battle.Net";
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
