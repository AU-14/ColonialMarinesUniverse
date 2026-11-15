using Content.Shared.CMU14.Yautja;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.CMU.Server.Yautja;

/// <summary>
/// CMU14: CM-SS13 refuses to let anybody but a Yautja or a thrall speak over yautja pack comms.
/// code/modules/cm_preds/yaut_items.dm:526 gates the yautja headset on
/// "if(!isyautja(M) && !isthrall(M)) //Nope.", and thralls hold TRAIT_YAUTJA_TECH
/// (yaut_hudprocs.dm:526), so carrying a stolen communicator is allowed but talking on the
/// pack channel is not.
/// </summary>
public sealed class YautjaCommsAccessSystem : EntitySystem
{
    private static readonly ProtoId<RadioChannelPrototype> YautjaChannel = "CMUYautja";

    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Channel.ID == SharedChatSystem.HivemindChannel.Id)
            return;

        if (args.Channel.ID != YautjaChannel.Id)
            return;

        if (CanUsePackComms(args.MessageSource))
            return;

        // CMU14: yaut_items.dm:526 refuses the transmission and tells the speaker why -
        // "You try to talk into the headset, but just get a horrible shrieking in your ears!". Carrying a
        // stolen communicator is allowed, talking on the pack channel is not, and now the attempt says so
        // instead of silently failing. This runs once per send, on the same guard that cancels the message.
        args.Cancelled = true;
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-comms-denied"),
            args.MessageSource,
            args.MessageSource,
            PopupType.SmallCaution);
    }

    /// <summary>
    /// CM-SS13's "!isyautja(M) && !isthrall(M)" test: a Yautja, a thrall, or any other tech-authorized
    /// user (all of whom hold TRAIT_YAUTJA_TECH) may transmit; nobody else may.
    /// </summary>
    public bool CanUsePackComms(EntityUid speaker)
    {
        return HasComp<YautjaComponent>(speaker) ||
               HasComp<YautjaThrallComponent>(speaker) ||
               HasComp<YautjaTechAuthorizedComponent>(speaker);
    }
}
