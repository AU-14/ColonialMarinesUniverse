using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaRitualSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly YautjaTrophySystem _trophy = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaRitualDuelComponent, ExaminedEvent>(OnRitualExamined);
        SubscribeLocalEvent<YautjaRitualDuelComponent, MobStateChangedEvent>(OnRitualPreyMobStateChanged);
        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyMobStateChanged);
    }

    private void OnRitualExamined(Entity<YautjaRitualDuelComponent> ent, ref ExaminedEvent args)
    {
        if (Deleted(ent.Comp.Hunter))
            return;

        var message = ent.Comp.State == YautjaRitualState.DuelActive
            ? "cmu-yautja-ritual-examine-duel"
            : "cmu-yautja-ritual-examine-captive";

        args.PushMarkup(Loc.GetString(message, ("hunter", HunterDisplayName(ent.Comp.Hunter))));
    }

    private void OnRitualPreyMobStateChanged(Entity<YautjaRitualDuelComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (ent.Comp.State == YautjaRitualState.DuelActive &&
            !Deleted(ent.Comp.Hunter) &&
            HasComp<YautjaComponent>(ent.Comp.Hunter))
        {
            _trophy.RecordRitualDuelWin(ent.Comp.Hunter, ent.Owner);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-duel-complete", ("target", ent.Owner)), ent.Comp.Hunter, ent.Comp.Hunter);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(ent.Comp.Hunter):hunter} completed a Yautja ritual duel against {ToPrettyString(ent.Owner):target}");
        }

        RemCompDeferred<YautjaRitualDuelComponent>(ent);
    }

    private void OnAnyMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !HasComp<YautjaComponent>(args.Target))
            return;

        var query = EntityQueryEnumerator<YautjaRitualDuelComponent>();
        while (query.MoveNext(out var uid, out var ritual))
        {
            if (ritual.Hunter == args.Target)
            {
                RemCompDeferred<YautjaRitualDuelComponent>(uid);
            }
        }
    }

    public bool TryClaimCaptive(EntityUid hunter, EntityUid target, bool bypassControlRequirement = false)
    {
        if (!CanClaimCaptive(hunter, target, bypassControlRequirement, true))
            return false;

        if (TryComp(target, out YautjaRitualDuelComponent? existing))
        {
            if (existing.Hunter == hunter)
                return true;

            _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-already-claimed"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        var ritual = EnsureComp<YautjaRitualDuelComponent>(target);
        ritual.Hunter = hunter;
        ritual.State = YautjaRitualState.Captive;
        ritual.CapturedAt = _timing.CurTime;
        ritual.DuelStartedAt = TimeSpan.Zero;

        _audio.PlayPvs(ritual.ClaimSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-captive-claimed", ("target", target)), hunter, hunter);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-captive-target", ("hunter", HunterDisplayName(hunter))), target, target, PopupType.MediumCaution);
        PopupToWitnesses(
            hunter,
            target,
            Loc.GetString("cmu-yautja-ritual-captive-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):hunter} claimed {ToPrettyString(target):target} as a Yautja ritual captive");
        return true;
    }

    public bool TryBeginDuel(EntityUid hunter, EntityUid target)
    {
        if (!TryComp(target, out YautjaRitualDuelComponent? ritual) ||
            ritual.Hunter != hunter ||
            ritual.State != YautjaRitualState.Captive ||
            !_mob.IsAlive(target))
        {
            return false;
        }

        ritual.State = YautjaRitualState.DuelActive;
        ritual.DuelStartedAt = _timing.CurTime;

        if (TryComp(target, out PullableComponent? pullable) && pullable.Puller == hunter)
            _pulling.TryStopPull(target, pullable, hunter);

        _audio.PlayPvs(ritual.DuelSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-duel-started", ("target", target)), hunter, hunter);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-duel-target", ("hunter", HunterDisplayName(hunter))), target, target, PopupType.MediumCaution);
        PopupToWitnesses(
            hunter,
            target,
            Loc.GetString("cmu-yautja-ritual-duel-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):hunter} began a Yautja ritual duel with {ToPrettyString(target):target}");
        return true;
    }

    public bool TryReleaseCaptive(EntityUid hunter, EntityUid target)
    {
        if (!TryComp(target, out YautjaRitualDuelComponent? ritual) ||
            ritual.Hunter != hunter)
        {
            return false;
        }

        _audio.PlayPvs(ritual.ReleaseSound, target);
        RemCompDeferred<YautjaRitualDuelComponent>(target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-released", ("target", target)), hunter, hunter);
        PopupToWitnesses(
            hunter,
            target,
            Loc.GetString("cmu-yautja-ritual-release-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(hunter):hunter} released Yautja ritual captive {ToPrettyString(target):target}");
        return true;
    }

    public bool CanClaimCaptive(EntityUid hunter, EntityUid target, bool bypassControlRequirement, bool popup)
    {
        if (Deleted(hunter) ||
            Deleted(target) ||
            hunter == target ||
            !HasComp<YautjaComponent>(hunter) ||
            HasComp<YautjaComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var mob) ||
            !_mob.IsAlive(target, mob) ||
            (!HasComp<HumanoidAppearanceComponent>(target) && !HasComp<XenoComponent>(target)))
        {
            return false;
        }

        if (bypassControlRequirement || IsPulling(hunter, target))
            return true;

        if (popup)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ritual-requires-control"), hunter, hunter, PopupType.SmallCaution);

        return false;
    }

    private bool IsPulling(EntityUid hunter, EntityUid target)
    {
        return TryComp<PullerComponent>(hunter, out var puller) && puller.Pulling == target;
    }

    private void PopupToWitnesses(EntityUid hunter, EntityUid target, string message, PopupType type)
    {
        var filter = Filter.Pvs(target, entityManager: EntityManager)
            .RemoveWhereAttachedEntity(attached => attached == hunter || attached == target);
        _popup.PopupEntity(message, target, filter, true, type);
    }

    private string HunterDisplayName(EntityUid hunter)
    {
        return HasComp<YautjaComponent>(hunter)
            ? Loc.GetString("cmu-yautja-identity-unknown")
            : Name(hunter);
    }
}
