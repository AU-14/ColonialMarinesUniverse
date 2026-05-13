using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaItemSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedXenoAcidSystem _acid = default!;
    [Dependency] private readonly YautjaThrallSystem _thralls = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCleanerComponent, AfterInteractEvent>(OnCleanerAfterInteract);
        SubscribeLocalEvent<YautjaCleanerComponent, YautjaCleanserDoAfterEvent>(OnCleanserDoAfter);

        SubscribeLocalEvent<YautjaHivebreakerComponent, AfterInteractEvent>(OnHivebreakerAfterInteract);
        SubscribeLocalEvent<YautjaHivebreakerComponent, YautjaHivebreakerDoAfterEvent>(OnHivebreakerDoAfter);
        SubscribeLocalEvent<XenoComponent, GetVerbsEvent<AlternativeVerb>>(OnGetXenoVerbs);

        SubscribeLocalEvent<YautjaRelayBeaconComponent, UseInHandEvent>(OnRelayBeaconUse);
        SubscribeLocalEvent<YautjaHoundPadComponent, UseInHandEvent>(OnHoundPadUse);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaDissolvingComponent>();
        while (query.MoveNext(out var uid, out var dissolving))
        {
            if (now < dissolving.DeleteAt)
                continue;

            if (HasComp<TimedCorrodingComponent>(uid))
                continue;

            _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-crumble", ("target", uid)), uid, PopupType.MediumCaution);
            QueueDel(uid);
        }
    }

    private void OnCleanerAfterInteract(Entity<YautjaCleanerComponent> cleaner, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartCleanser(cleaner, args.User, target, args.CanReach);
    }

    private bool TryStartCleanser(Entity<YautjaCleanerComponent> cleaner, EntityUid user, EntityUid target, bool canReach)
    {
        if (!CanUseYautjaTech(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!canReach || !CanDissolve(cleaner.Owner, target, user, true))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            cleaner.Comp.DoAfter,
            new YautjaCleanserDoAfterEvent(),
            cleaner.Owner,
            target: target,
            used: cleaner.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPvs(cleaner.Comp.StartSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-start-self", ("target", target)), user, user, PopupType.LargeCaution);
        return true;
    }

    private void OnCleanserDoAfter(Entity<YautjaCleanerComponent> cleaner, ref YautjaCleanserDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanDissolve(cleaner.Owner, target, args.User, true))
            return;

        var dissolving = EnsureComp<YautjaDissolvingComponent>(target);
        dissolving.DeleteAt = _timing.CurTime + cleaner.Comp.DissolveDelay;

        _acid.ApplyAcid(
            cleaner.Comp.AcidPrototype,
            cleaner.Comp.AcidStrength,
            target,
            cleaner.Comp.AcidDps,
            cleaner.Comp.LightAcidDps,
            cleaner.Comp.DissolveDelay);

        _audio.PlayPvs(cleaner.Comp.FinishSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-covered", ("target", target)), args.User, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} covered {ToPrettyString(target):target} in Yautja dissolving gel");
    }

    private bool CanDissolve(EntityUid cleaner, EntityUid target, EntityUid user, bool popup)
    {
        if (target == cleaner ||
            Deleted(target) ||
            !HasComp<ItemComponent>(target) ||
            HasComp<YautjaCleanerComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-invalid"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<YautjaDissolvingComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-already"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (_acid.IsMelted(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-already"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (TryComp(target, out TransformComponent? xform))
        {
            if (xform.Anchored)
            {
                if (popup)
                    _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-anchored"), user, user, PopupType.SmallCaution);
                return false;
            }

            if (HasComp<MobStateComponent>(xform.ParentUid))
            {
                if (popup)
                    _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-held"), user, user, PopupType.SmallCaution);
                return false;
            }
        }

        return true;
    }

    private void OnHivebreakerAfterInteract(Entity<YautjaHivebreakerComponent> hivebreaker, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartHivebreaker(hivebreaker, args.User, target, args.CanReach);
    }

    private void OnGetXenoVerbs(Entity<XenoComponent> xeno, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            _hands.GetActiveItem(args.User) is not { } held ||
            !TryComp(held, out YautjaHivebreakerComponent? hivebreaker))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-yautja-hivebreaker-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Priority = 3,
            Act = () => TryStartHivebreaker((held, hivebreaker), user, xeno.Owner, true),
        });
    }

    private bool TryStartHivebreaker(Entity<YautjaHivebreakerComponent> hivebreaker, EntityUid user, EntityUid target, bool canReach)
    {
        if (!canReach || !CanHivebreak(hivebreaker.Comp, user, target, true))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            hivebreaker.Comp.DoAfter,
            new YautjaHivebreakerDoAfterEvent(),
            hivebreaker.Owner,
            target: target,
            used: hivebreaker.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPvs(hivebreaker.Comp.StartSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-start-self", ("target", target)), user, user, PopupType.LargeCaution);
        return true;
    }

    private void OnHivebreakerDoAfter(Entity<YautjaHivebreakerComponent> hivebreaker, ref YautjaHivebreakerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanHivebreak(hivebreaker.Comp, args.User, target, true))
            return;

        _thralls.HivebreakXeno(args.User, target, hivebreaker.Owner, hivebreaker.Comp);

        hivebreaker.Comp.Uses--;
        _audio.PlayPvs(hivebreaker.Comp.FinishSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-finished-self", ("target", target)), args.User, args.User);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-finished-target", ("hunter", args.User)), target, target, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(args.User):hunter} enthralled xeno {ToPrettyString(target):target} with a Yautja hivebreaker");

        if (hivebreaker.Comp.Uses <= 0)
            QueueDel(hivebreaker.Owner);
    }

    private bool CanHivebreak(YautjaHivebreakerComponent hivebreaker, EntityUid user, EntityUid target, bool popup)
    {
        if (Deleted(target) || !HasComp<XenoComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-requires-xeno"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<YautjaThrallComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-already"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HunterHasAnotherThrall(user, target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-already-has"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (hivebreaker.RequireCritical && !_mobState.IsCritical(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-requires-critical"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool HunterHasAnotherThrall(EntityUid hunter, EntityUid target)
    {
        var query = EntityQueryEnumerator<YautjaThrallComponent>();
        while (query.MoveNext(out var uid, out var thrall))
        {
            if (uid == target ||
                thrall.Master != hunter ||
                Deleted(uid) ||
                _mobState.IsDead(uid))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void OnRelayBeaconUse(Entity<YautjaRelayBeaconComponent> beacon, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(beacon.Comp.PulseSound, beacon.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-relay-beacon-pulse"), args.User, args.User);
    }

    private void OnHoundPadUse(Entity<YautjaHoundPadComponent> pad, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(pad.Comp.PulseSound, pad.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-houndpad-pulse"), args.User, args.User);
    }

    private bool CanUseYautjaTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) || HasComp<YautjaTechAuthorizedComponent>(user);
    }
}
