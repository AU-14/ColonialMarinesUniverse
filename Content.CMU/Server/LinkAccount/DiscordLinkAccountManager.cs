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
/// Discord account linking without a separate bot process: players post the code they got in the
/// lobby (top left) into the configured channel, and the posting Discord user gets linked. Rides
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
        var channel = _config.GetCVar(CCVars.DiscordLinkChannel);
        if (channel == 0 || message.ChannelId != channel || message.Author.IsBot)
            return;

        var match = CodeRegex.Match(message.Content);
        if (!match.Success)
            return; // not a linking attempt; leave ordinary channel chatter alone

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
    }
}
