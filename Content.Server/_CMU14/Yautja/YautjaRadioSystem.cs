using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Chat;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaRadioSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private MobStateSystem _mob = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCommunicatorComponent, RadioSendAttemptEvent>(OnCommunicatorSendAttempt);
    }

    private void OnCommunicatorSendAttempt(Entity<YautjaCommunicatorComponent> ent, ref RadioSendAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<YautjaComponent>(args.MessageSource) &&
            !HasComp<YautjaThrallComponent>(args.MessageSource))
        {
            args.Cancelled = true;
            return;
        }

        if (args.Channel.ID == ent.Comp.RegularChannel)
        {
            ForwardToHellhounds(args, ent.Comp.RegularFaction, true);
        }
        else if (args.Channel.ID == ent.Comp.StrandedChannel)
        {
            ForwardToHellhounds(args, ent.Comp.StrandedFaction, false);
        }
        else if (args.Channel.ID == ent.Comp.BadBloodChannel)
        {
            ForwardToHellhounds(args, ent.Comp.BadBloodFaction, false);
            ForwardToBadBloodHivebrokenXenos(args, ent.Comp.BadBloodFaction);
        }
    }

    private void ForwardToHellhounds(
        RadioSendAttemptEvent args,
        ProtoId<NpcFactionPrototype> faction,
        bool missingFactionCountsAsMatch)
    {
        var query = EntityQueryEnumerator<YautjaHellhoundComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Deleted(uid) ||
                !_mob.IsAlive(uid) ||
                !HasFaction(uid, faction, missingFactionCountsAsMatch))
            {
                continue;
            }

            SendDirectRadio(uid, args, CreateDirectRadioChatMessage(args));
        }
    }

    private void ForwardToBadBloodHivebrokenXenos(RadioSendAttemptEvent args, ProtoId<NpcFactionPrototype> faction)
    {
        var query = EntityQueryEnumerator<YautjaHivebrokenXenoComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Deleted(uid) ||
                !_mob.IsAlive(uid) ||
                !HasFaction(uid, faction, false))
            {
                continue;
            }

            SendDirectRadio(uid, args, CreateDirectRadioChatMessage(args));
        }
    }

    private bool HasFaction(EntityUid uid, ProtoId<NpcFactionPrototype> faction, bool missingFactionCountsAsMatch)
    {
        if (!TryComp(uid, out NpcFactionMemberComponent? member))
            return missingFactionCountsAsMatch;

        return member.Factions.Contains(faction);
    }

    private void SendDirectRadio(EntityUid receiver, RadioSendAttemptEvent args, MsgChatMessage? chatMsg = null)
    {
        chatMsg ??= args.ChatMsg;
        var ev = new RadioReceiveEvent(
            args.Message,
            args.MessageSource,
            args.Channel,
            args.RadioSource,
            chatMsg,
            SharedLanguageSystem.CommonLanguage);
        RaiseLocalEvent(receiver, ref ev);

        if (!HasComp<IntrinsicRadioReceiverComponent>(receiver) &&
            _players.TryGetSessionByEntity(receiver, out var session))
        {
            _net.ServerSendMessage(chatMsg, session.Channel);
        }
    }

    private MsgChatMessage CreateDirectRadioChatMessage(RadioSendAttemptEvent args)
    {
        const string verb = "commands";
        const string channelLabel = "Radio";

        var senderName = FormattedMessage.EscapeText(Name(args.MessageSource));
        var message = FormattedMessage.EscapeText(args.Message);
        var wrappedMessage = $"[bold]\\[{channelLabel}\\]: {senderName} {verb}, '[bold]{message}[/bold]'.[/bold]";

        var chat = new ChatMessage(
            ChatChannel.Radio,
            args.Message,
            wrappedMessage,
            args.ChatMsg.Message.SenderEntity,
            args.ChatMsg.Message.SenderKey,
            repeatCheckSender: args.ChatMsg.Message.RepeatCheckSender,
            display: new ChatDisplayMetadata(
                ChatDisplayKind.Radio,
                senderName: senderName,
                verb: verb,
                channelLabel: channelLabel,
                quoteBody: true,
                accentColor: args.Channel.Color));

        return new MsgChatMessage { Message = chat };
    }
}
