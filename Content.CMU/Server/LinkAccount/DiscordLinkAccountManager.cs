using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server.Discord.DiscordLink;
using Content.Server.Database;
using Content.Shared.CCVar;
using NetCord;
using NetCord.Gateway;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Server.CMU14.LinkAccount;

/// <summary>
/// Discord account linking without a separate bot process: players DM the code they got in the
/// lobby (top left) to this bot, and the DM's author gets linked; posting the code into the
/// configured channel works too and exists for admin-mediated linking. Rides
/// the in-process <see cref="DiscordLink"/> gateway, so it comes alive purely through CVars and
/// ships with the normal publish pipeline. Codes are single-use and expire, and the link write
/// happens through the server's own database connection — nothing sensitive leaves the box.
/// Replaces the stubbed-out Content.DiscordBot flow.
/// </summary>
public sealed class DiscordLinkAccountManager : IPostInjectInit
{
    private static readonly Regex CodeRegex = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);

    private static readonly TimeSpan MinimumAttemptWait = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<ulong, long> _nextAttemptMs = new();

    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly DiscordLink _discord = default!;

    private ISawmill _sawmill = default!;

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("discord.linkaccount");
        // Safe before DiscordLink.Initialize: the event exists on the constructed client, and
        // messages only arrive once the gateway connects.
        _discord.OnMessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(Message message)
    {
        // IsBot misses webhooks; WebhookId is the documented check.
        if (message.Author.IsBot || message.WebhookId is not null)
            return;

        var channel = _config.GetCVar(CCVars.DiscordLinkChannel);
        if (channel == 0)
            return;

        // DMs carry codes where nobody else can read them; the configured
        // channel stays as the admin-mediated path.
        if (message.ChannelId != channel && message.GuildId != null)
            return;

        var match = CodeRegex.Match(message.Content);
        if (!match.Success)
            return; // not a linking attempt; leave ordinary chatter alone

        // One attempt per author per window: the database probe and the bot reply are both free
        // to abuse otherwise. Silent while cooling, so a flood buys nothing observable.
        var now = Environment.TickCount64;
        if (_nextAttemptMs.TryGetValue(message.Author.Id, out var allowed) && now < allowed)
            return;
        _nextAttemptMs[message.Author.Id] = now + (long)MinimumAttemptWait.TotalMilliseconds;

        if (_nextAttemptMs.Count > 512)
        {
            foreach (var (author, deadline) in _nextAttemptMs)
            {
                if (now >= deadline)
                    _nextAttemptMs.TryRemove(author, out _);
            }
        }

        _ = HandleMessageAsync(message, Guid.Parse(match.Value));
    }

    private async Task HandleMessageAsync(Message message, Guid code)
    {
        var discordId = message.Author.Id;
        DiscordLinkResult result;
        try
        {
            result = await _db.LinkDiscordAccountByCode(code, discordId);
        }
        catch (Exception e)
        {
            // a database failure must not read to the player as an ignored code, and must not go unlogged
            _sawmill.Error($"failed to link discord {discordId} by code:\n{e}");
            return;
        }

        switch (result.Status)
        {
            case DiscordLinkStatus.Success:
                _sawmill.Info($"linked player {result.UserName} ({result.PlayerId}) to discord {discordId}");
                break;
            case DiscordLinkStatus.ExpiredCode:
                _sawmill.Info($"discord {discordId} posted an expired linking code");
                break;
            case DiscordLinkStatus.UnknownCode:
                _sawmill.Info($"discord {discordId} posted an unknown linking code");
                break;
        }

        var reply = result.Status switch
        {
            DiscordLinkStatus.Success => $"Linked SS14 account '{result.UserName}'.",
            DiscordLinkStatus.ExpiredCode => "That code expired — join the server and request a new one from the lobby (top left).",
            _ => "No matching code — join the server and request one from the lobby (top left), then post it here.",
        };

        try
        {
            await _discord.SendMessageAsync(message.ChannelId, reply);
        }
        catch (Exception e)
        {
            _sawmill.Error($"failed to send linking reply to channel {message.ChannelId}:\n{e}");
        }

        // DM-linked successes are invisible to staff: mirror them into the configured channel for
        // bookkeeping. Channel-originated links already post where staff can read them.
        var staffChannel = _config.GetCVar(CCVars.DiscordLinkChannel);
        if (result.Status == DiscordLinkStatus.Success
            && message.ChannelId != staffChannel
            && staffChannel != 0)
        {
            try
            {
                await _discord.SendMessageAsync(staffChannel,
                    $"Linked <@{discordId}> ({message.Author.Username}) to SS14 account '{result.UserName}'.");
            }
            catch (Exception e)
            {
                _sawmill.Error($"failed to send link bookkeeping message to channel {staffChannel}:\n{e}");
            }
        }
    }
}
