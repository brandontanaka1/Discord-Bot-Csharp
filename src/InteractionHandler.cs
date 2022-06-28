using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.Interactions;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Discord;
using System.Linq;
using Newtonsoft.Json;

namespace Discord_Bot
{
    public class InteractionHandlingService
    {
        private readonly InteractionService _interactions;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;

        public InteractionHandlingService(IServiceProvider services)
        {

            _interactions = services.GetRequiredService<InteractionService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _services = services;

            // Event handlers
            _client.Ready += ClientReadyAsync;
            _client.InteractionCreated += HandleInteractionAsync;
        }

        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);

            var result = await _interactions.ExecuteCommandAsync(context, _services);

                if (!result.IsSuccess && result.Error.HasValue)
                    await context.Channel.SendMessageAsync($":x: {result.ErrorReason}");
        }

        private async Task ClientReadyAsync()
            => await Functions.SetBotStatusAsync(_client);

        public async Task InitializeAsync()
            => await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}