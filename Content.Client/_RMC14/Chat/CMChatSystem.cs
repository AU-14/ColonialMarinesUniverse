using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Chat;
using Content.Shared.Chat;
using System.Collections.Generic;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Chat;

public sealed class CMChatSystem : SharedCMChatSystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;

    private int _repeatHistory;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, RMCCVars.RMCChatRepeatHistory, v => _repeatHistory = v, true);
    }

    public bool TryRepetition(Queue<RepeatedMessage> repeatQueue, NetEntity sender, string unwrapped, ChatChannel channel, bool repeatCheckSender)
    {
        foreach (var old in repeatQueue)
        {
            if (!old.Message.Equals(unwrapped) ||
                old.Channel != channel)
            {
                continue;
            }

            if (repeatCheckSender &&
                !old.SenderEntity.Equals(sender))
            {
                continue;
            }

            old.Count++;
            old.Row?.SetRepeatCount(old.Count);
            return true;
        }

        return false;
    }

    public bool TryLegacyRepetition(Queue<RepeatedMessage> repeatQueue, OutputPanel contents, FormattedMessage message, NetEntity sender, string unwrapped, ChatChannel channel, bool repeatCheckSender)
    {
        foreach (var old in repeatQueue)
        {
            if (!old.Message.Equals(unwrapped) ||
                old.Channel != channel)
            {
                continue;
            }

            if (repeatCheckSender &&
                !old.SenderEntity.Equals(sender))
            {
                continue;
            }

            var copy = new FormattedMessage(old.FormattedMessage);
            old.Count++;
            copy.AddMarkupPermissive($" [color=red]x{old.Count}[/color]");
            contents.SetMessage(old.Index, copy);
            return true;
        }

        return false;
    }

    public void TrackRepetition(Queue<RepeatedMessage> repeatQueue, ChatMessageRow row, FormattedMessage message, NetEntity sender, string unwrapped, ChatChannel channel)
    {
        repeatQueue.Enqueue(new RepeatedMessage(row, message, sender, unwrapped, channel));
        TrimRepeatQueue(repeatQueue);
    }

    public void TrackLegacyRepetition(Queue<RepeatedMessage> repeatQueue, OutputPanel contents, FormattedMessage message, NetEntity sender, string unwrapped, ChatChannel channel)
    {
        repeatQueue.Enqueue(new RepeatedMessage(contents.EntryCount, message, sender, unwrapped, channel));
        TrimRepeatQueue(repeatQueue);
    }

    private void TrimRepeatQueue(Queue<RepeatedMessage> repeatQueue)
    {
        if (_repeatHistory <= 0)
            return;

        while (repeatQueue.Count > _repeatHistory)
        {
            repeatQueue.Dequeue();
        }
    }
}
