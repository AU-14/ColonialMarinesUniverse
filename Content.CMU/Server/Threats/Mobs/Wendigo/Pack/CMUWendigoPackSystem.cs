using Content.Server._RMC14.Announce;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Chat;
using Content.Shared.Chat;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Pack;

/// <summary>
/// Server side of the Wendigo pack: the private pack radio and the death announcement.
/// Friendly fire lives in <see cref="SharedCMUWendigoPackSystem"/>; watching lives in the CMU partial
/// of <c>XenoWatchSystem</c>.
/// </summary>
public sealed partial class CMUWendigoPackSystem : EntitySystem
{
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private XenoAnnounceSystem _xenoAnnounce = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUWendigoPackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUWendigoPackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUWendigoPackComponent, GetDefaultRadioChannelEvent>(OnGetDefaultRadioChannel);
        SubscribeLocalEvent<CMUWendigoPackComponent, ChatGetPrefixEvent>(OnGetPrefix);
        SubscribeLocalEvent<CMUWendigoPackComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(Entity<CMUWendigoPackComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<IntrinsicRadioReceiverComponent>(ent);

        // A fresh transmitter defaults to Common; Wendigos only speak on the pack band.
        if (!EnsureComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
            transmitter.Channels.Clear();

        transmitter.Channels.Add(ent.Comp.Channel);
        EnsureComp<ActiveRadioComponent>(ent).Channels.Add(ent.Comp.Channel);
    }

    private void OnShutdown(Entity<CMUWendigoPackComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp(ent, out IntrinsicRadioTransmitterComponent? transmitter))
            transmitter.Channels.Remove(ent.Comp.Channel);

        if (TryComp(ent, out ActiveRadioComponent? active))
            active.Channels.Remove(ent.Comp.Channel);
    }

    private void OnGetDefaultRadioChannel(Entity<CMUWendigoPackComponent> ent, ref GetDefaultRadioChannelEvent args)
    {
        args.Channel = ent.Comp.Channel;
    }

    private void OnGetPrefix(Entity<CMUWendigoPackComponent> ent, ref ChatGetPrefixEvent args)
    {
        // Wendigos are not part of any hive.
        if (args.Channel?.ID == SharedChatSystem.HivemindChannel.Id)
            args.Channel = null;
    }

    private void OnMobStateChanged(Entity<CMUWendigoPackComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var location = Loc.GetString("cmu-wendigo-pack-unknown-location");
        if (_areas.TryGetArea(ent.Owner, out _, out var areaProto))
            location = areaProto.Name;

        var message = Loc.GetString(ent.Comp.DeathAnnouncement, ("wendigo", ent.Owner), ("location", location));
        var dead = ent.Owner;
        var filter = Filter.Empty().AddWhereAttachedEntity(e =>
            e != dead && HasComp<CMUWendigoPackComponent>(e) && !_mobState.IsDead(e));

        _xenoAnnounce.Announce(ent, filter, message, _xenoAnnounce.WrapHive(message, ent.Comp.AnnouncementColor));
    }
}
