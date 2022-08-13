﻿using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord_Bot_Csharp.src.Data_Access;
using MongoDB.Driver.Linq;
using MongoDB.Driver;
using System.Linq;
using System.Collections.Generic;

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
            component = component.WithButton("Start Tracking", style: ButtonStyle.Primary, customId: "tracking");
            component = component.WithButton("Stop Tracking", style: ButtonStyle.Secondary, customId: "runinteraction:stop");

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
                    formattedReply = $"**{runner.Name}** has started a new run!\n**Platform:** {currentGame.Platform}\n**Game Type:** {currentGame.GameType}\n**Game Name:** {currentGame.GameName}\n**Game Password:** {currentGame.GamePassword}\n\n";
                }
            }

            await ReplyAsync(formattedReply);
        }

        [Command("leaderboards")]
        [Discord.Commands.Summary("Display leaderboards")]
        public async Task DisplayLeaderboards([Remainder] string message = "")
        {
            var statsController = new BaseDataController<Diablo2RunnerStats>(ConnectionString);
            var statsTask = statsController.GetQuery().GroupBy(stats => stats.RunnerName).Select(x => new LeaderboardDto { Key = x.Key, Total = x.Sum(y => y.RunCount) }).OrderByDescending(x => x.Total).Take(5).ToListAsync();
            var chaosStatsTask = statsController.GetQuery().Where(stat => stat.RunType == "chaos").GroupBy(stats => stats.RunnerName).Select(x => new LeaderboardDto { Key = x.Key, Total = x.Sum(y => y.RunCount) }).OrderByDescending(x => x.Total).Take(5).ToListAsync();
            var baalStatsTask = statsController.GetQuery().Where(stat => stat.RunType == "baal").GroupBy(stats => stats.RunnerName).Select(x => new LeaderboardDto { Key = x.Key, Total = x.Sum(y => y.RunCount) }).OrderByDescending(x => x.Total).Take(5).ToListAsync();
            var splitMfStatsTask = statsController.GetQuery().Where(stat => stat.RunType == "split-mf").GroupBy(stats => stats.RunnerName).Select(x => new LeaderboardDto { Key = x.Key, Total = x.Sum(y => y.RunCount) }).OrderByDescending(x => x.Total).Take(5).ToListAsync();
            var cowStatsTask = statsController.GetQuery().Where(stat => stat.RunType == "cow").GroupBy(stats => stats.RunnerName).Select(x => new LeaderboardDto { Key = x.Key, Total = x.Sum(y => y.RunCount) }).OrderByDescending(x => x.Total).Take(5).ToListAsync();

            var stats = await statsTask;
            var chaosStats = await chaosStatsTask;
            var baalStats = await baalStatsTask;
            var splitMfStats = await splitMfStatsTask;
            var cowStats = await cowStatsTask;
            var embed = new EmbedBuilder();
            embed.WithDescription("**Top 5 runners per type**");
            embed.WithTitle("The Throne Room Leaderboards");
            BuildTop5List(embed, "Overall", stats);
            BuildTop5List(embed, "Chaos", chaosStats);
            BuildTop5List(embed, "Baal", baalStats);
            BuildTop5List(embed, "Split MF", splitMfStats);
            BuildTop5List(embed, "Cow", cowStats);

            await Context.Channel.SendMessageAsync("", false, embed.Build());
        }

        [Command("addruntype")]
        [Discord.Commands.Summary("Add Run Type")]
        public async Task AddRunType([Remainder] string message = "")
        {
            try
            {
                var split = message.Split(" ");
                var runType = new RunType() { Name = split[0].Trim(), Value = split[1].Trim(), Channel = long.Parse(split[2].Trim()) };
                var runTypeController = new BaseDataController<RunType>(ConnectionString);
                await runTypeController.GetCollection().InsertOneAsync(runType);
            }
            catch (Exception)
            {
                await ReplyAsync("Error encountered running command. Command must be in format:\n$addruntype {Name} {Value} {Channel ID}\n$addruntype Chaos chaos 991333288063012965");
            }
        }

        private void BuildTop5List(EmbedBuilder embed, string embedName, List<LeaderboardDto> leaderboardDtos)
        {
            if (leaderboardDtos.Count == 0)
            {
                return;
            }

            string reply = "";
            var count = 1;

            foreach (var stat in leaderboardDtos)
            {
                if (stat != null)
                {
                    reply += $"{count}. {stat.Key} - {stat.Total}\n";
                    count++;
                }
            }

            var embedFieldBuilder = new EmbedFieldBuilder()
            {
                Name = embedName,
                Value = reply,
                IsInline = false
            };

            embed.AddField(embedFieldBuilder);
        }

        [Command("commands")]
        [Discord.Commands.Summary("Display commands")]
        public async Task DisplayCommands([Remainder] string message = "")
        {
            await ReplyAsync($"Supported commands:\n**$tracking**\n**$currentruns**\n**$leaderboards**");
        }
    }

    public class LeaderboardDto
    {
        public string Key { get; set; }

        public long Total { get; set; }
    }
}
