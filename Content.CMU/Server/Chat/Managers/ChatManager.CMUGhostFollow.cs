// ReSharper disable CheckNamespace

using Content.Server.Ghost;
using Content.Server._RMC14.Xenonids.Watch;
using Content.Shared._CMU14.Ghost;
using Content.Shared._CMU14.Xenonids.Watch;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CCVar;
using Robust.Shared.Network;

namespace Content.Server.Chat.Managers;

internal sealed partial class ChatManager
{
    public string AddGhostFollowButton(string wrappedMessage, EntityUid source, INetChannel recipient)
    {
        if (!TryCreateGhostFollowButton(wrappedMessage, source, recipient, out var customWrappedMessage, out _))
            return wrappedMessage;

        return customWrappedMessage;
    }

    public string AddXenoWatchButton(string wrappedMessage, EntityUid source, INetChannel recipient)
    {
        if (!TryCreateXenoWatchButton(wrappedMessage, source, recipient, out var customWrappedMessage, out _))
            return wrappedMessage;

        return customWrappedMessage;
    }

    private bool TryCreateGhostFollowButton(
        string wrappedMessage,
        EntityUid source,
        INetChannel recipient,
        out string customWrappedMessage,
        out NetEntity followEntity)
    {
        customWrappedMessage = wrappedMessage;
        followEntity = default;

        if (!source.Valid || !ShouldShowGhostFollowButton(recipient))
            return false;

        followEntity = _entityManager.GetNetEntity(source);
        var buttonText = Loc.GetString("cmu-chat-manager-follow-button");
        customWrappedMessage = $"[cmdlink=\"{buttonText}\" command=\"{CMUGhostFollowCommand.CommandName} {followEntity}\" /] {wrappedMessage}";
        return true;
    }

    private bool ShouldShowGhostFollowButton(INetChannel recipient)
    {
        if (!_player.TryGetSessionByChannel(recipient, out var session))
            return false;

        if (!_entityManager.TrySystem(out GhostSystem? ghost) ||
            !ghost.CanGhostFollow(session, out _))
        {
            return false;
        }

        return _netConfigManager.GetClientCVar(recipient, CCVars.ChatGhostFollowButton);
    }

    private bool TryCreateXenoWatchButton(
        string wrappedMessage,
        EntityUid source,
        INetChannel recipient,
        out string customWrappedMessage,
        out NetEntity watchEntity)
    {
        customWrappedMessage = wrappedMessage;
        watchEntity = default;

        if (!source.Valid ||
            !_entityManager.HasComponent<XenoComponent>(source) ||
            !_player.TryGetSessionByChannel(recipient, out var session) ||
            !_entityManager.TrySystem(out XenoWatchSystem? watch) ||
            !watch.CanXenoWatch(session, out var watcher) ||
            watcher == source)
        {
            return false;
        }

        watchEntity = _entityManager.GetNetEntity(source);
        var buttonText = Loc.GetString("cmu-chat-manager-xeno-watch-button");
        customWrappedMessage = $"[cmdlink=\"{buttonText}\" command=\"{CMUXenoWatchCommand.CommandName} {watchEntity}\" /] {wrappedMessage}";
        return true;
    }
}
