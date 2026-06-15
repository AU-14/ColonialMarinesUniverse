// ReSharper disable CheckNamespace

using Content.Server.Ghost;
using Content.Shared.CCVar;
using Robust.Shared.Network;

namespace Content.Server.Chat.Managers;

internal sealed partial class ChatManager
{
    public string AddGhostFollowButton(string wrappedMessage, EntityUid source, INetChannel recipient)
    {
        if (!source.Valid || !ShouldShowGhostFollowButton(recipient))
            return wrappedMessage;

        var buttonText = Loc.GetString("cmu-chat-manager-follow-button");
        return $"[cmdlink=\"{buttonText}\" command=\"{CMUGhostFollowEntityCommand.CommandName} {_entityManager.GetNetEntity(source)}\" /] {wrappedMessage}";
    }

    private bool ShouldShowGhostFollowButton(INetChannel recipient)
    {
        if (!_player.TryGetSessionByChannel(recipient, out var session))
            return false;

        if (!_entityManager.TrySystem(out GhostSystem? ghost) ||
            !ghost.CanGhostWarp(session, out _))
        {
            return false;
        }

        return _netConfigManager.GetClientCVar(recipient, CCVars.ChatGhostFollowButton);
    }
}
