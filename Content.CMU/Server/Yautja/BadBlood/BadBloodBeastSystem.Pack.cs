using System.Numerics;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Content.Shared.Actions;

namespace Content.Server.CMU14.Yautja.BadBlood;

// CMU14 ECS-707: the Beast-Master's pack actions, on the pack layer so they cover hounds and thralls alike.
// Blood Scent (a mark: x1.35 from every bonded beast), Rally Howl (+20% speed to the pack nearby), Mend Beast (a
// do-after heal, once a minute per beast), Pack Bond (bonded beasts nearby regenerate x1.5, and the clan hound camera
// is built in) and Last Order (BadBloodBeastSystem.OnMasterMobStateChanged). STATE-702 war-paint lives here too.
// Dependency fields taken by this file: _packActions, _packDoAfter, _packLookup, _packThresholds, _packTransform,
// _packUi, _packXeno.
public sealed partial class BadBloodBeastSystem
{
    // RMC's xeno regeneration while standing, per second: (incap threshold / 65 + flat 0.5) x stand 0.4 / 2
    private const float StandRegenFlat = 0.5f;
    private const float StandRegenShare = 0.2f;

    [Dependency] private SharedActionsSystem _packActions = default!;
    [Dependency] private SharedDoAfterSystem _packDoAfter = default!;
    [Dependency] private MobThresholdSystem _packThresholds = default!;
    [Dependency] private SharedTransformSystem _packTransform = default!;
    [Dependency] private SharedUserInterfaceSystem _packUi = default!;
    [Dependency] private XenoSystem _packXeno = default!;

    private readonly List<EntityUid> _expiredScratch = new();

    private void InitializePack()
    {
        SubscribeLocalEvent<BadBloodBeastMasterComponent, MapInitEvent>(OnMasterMapInit);
        SubscribeLocalEvent<BadBloodBeastMasterComponent, ComponentShutdown>(OnMasterShutdown);
        SubscribeLocalEvent<BadBloodBeastMasterComponent, BadBloodBloodScentActionEvent>(OnBloodScentAction);
        SubscribeLocalEvent<BadBloodBeastMasterComponent, BadBloodRallyHowlActionEvent>(OnRallyHowlAction);
        SubscribeLocalEvent<BadBloodBeastMasterComponent, BadBloodMendBeastActionEvent>(OnMendBeastAction);
        SubscribeLocalEvent<BadBloodBeastMasterComponent, BadBloodMendBeastDoAfterEvent>(OnMendBeastDoAfter);
        SubscribeLocalEvent<BadBloodBeastMasterComponent, BadBloodPackSightActionEvent>(OnPackSightAction);

        SubscribeLocalEvent<BadBloodBloodScentComponent, DamageModifyEvent>(OnScentDamageModify);
        SubscribeLocalEvent<BadBloodRallyHowlComponent, RefreshMovementSpeedModifiersEvent>(OnRallyRefreshSpeed);
        SubscribeLocalEvent<BadBloodWarPaintComponent, GetMeleeDamageEvent>(OnWarPaintMeleeDamage);
    }

    #region Lifecycle

    private void OnMasterMapInit(Entity<BadBloodBeastMasterComponent> ent, ref MapInitEvent args)
    {
        _packActions.AddAction(ent.Owner, ref ent.Comp.BloodScentActionEntity, ent.Comp.BloodScentAction);
        _packActions.AddAction(ent.Owner, ref ent.Comp.RallyHowlActionEntity, ent.Comp.RallyHowlAction);
        _packActions.AddAction(ent.Owner, ref ent.Comp.MendBeastActionEntity, ent.Comp.MendBeastAction);
        _packActions.AddAction(ent.Owner, ref ent.Comp.PackSightActionEntity, ent.Comp.PackSightAction);
        Dirty(ent);
    }

    // the caste is dropped or the body is going: the actions and the camera go, the pack goes to the next master
    private void OnMasterShutdown(Entity<BadBloodBeastMasterComponent> ent, ref ComponentShutdown args)
    {
        _packActions.RemoveAction(ent.Owner, ent.Comp.BloodScentActionEntity);
        _packActions.RemoveAction(ent.Owner, ent.Comp.RallyHowlActionEntity);
        _packActions.RemoveAction(ent.Owner, ent.Comp.MendBeastActionEntity);
        _packActions.RemoveAction(ent.Owner, ent.Comp.PackSightActionEntity);

        if (ent.Comp.PackSightCamera is { } camera && !TerminatingOrDeleted(camera))
            QueueDel(camera);

        var now = _timing.CurTime;
        foreach (var beast in ent.Comp.Beasts)
        {
            if (TryComp(beast, out BadBloodBeastComponent? comp) && comp.Master == ent.Owner && comp.RebindAt == null)
                comp.RebindAt = now;
        }
    }

    #endregion

    #region Blood Scent

    private void OnBloodScentAction(Entity<BadBloodBeastMasterComponent> ent, ref BadBloodBloodScentActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (target == ent.Owner ||
            !HasComp<MobStateComponent>(target) ||
            _mobState.IsDead(target) ||
            HasComp<YautjaBadBloodComponent>(target) ||
            IsClanBeast(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-beast-scent-invalid"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        var scent = EnsureComp<BadBloodBloodScentComponent>(target);
        scent.Master = ent.Owner;
        scent.Multiplier = ent.Comp.BloodScentMultiplier;
        scent.ExpiresAt = _timing.CurTime + ent.Comp.BloodScentDuration;
        Dirty(target, scent);

        var seconds = (int) ent.Comp.BloodScentDuration.TotalSeconds;
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-beast-scent-master", ("target", target), ("seconds", seconds)),
            ent, ent, PopupType.Medium);
        foreach (var beast in ent.Comp.Beasts)
        {
            if (!TerminatingOrDeleted(beast))
                _popup.PopupEntity(Loc.GetString("cmu-bad-blood-beast-scent-beast", ("target", target)), beast, beast, PopupType.LargeCaution);
        }
    }

    // a hit from a beast bonded to the Beast-Master who laid the scent
    private void OnScentDamageModify(Entity<BadBloodBloodScentComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } attacker ||
            !TryComp(attacker, out BadBloodBeastComponent? beast) ||
            beast.Master != ent.Comp.Master)
        {
            return;
        }

        args.Damage *= ent.Comp.Multiplier;
    }

    #endregion

    #region Rally Howl

    private void OnRallyHowlAction(Entity<BadBloodBeastMasterComponent> ent, ref BadBloodRallyHowlActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var here = _packTransform.GetMapCoordinates(ent.Owner);
        var until = _timing.CurTime + ent.Comp.RallyDuration;
        var rallied = 0;
        foreach (var beast in ent.Comp.Beasts)
        {
            if (TerminatingOrDeleted(beast) || _mobState.IsDead(beast) || !InRange(beast, here, ent.Comp.RallyRadius))
                continue;

            var rally = EnsureComp<BadBloodRallyHowlComponent>(beast);
            rally.ExpiresAt = until;
            rally.SpeedMultiplier = ent.Comp.RallySpeedMultiplier;
            Dirty(beast, rally);
            _speed.RefreshMovementSpeedModifiers(beast);
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-beast-rally-beast", ("master", ent.Owner)), beast, beast, PopupType.Medium);
            rallied++;
        }

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-beast-rally-master", ("count", rallied)), ent, PopupType.Medium);
    }

    private void OnRallyRefreshSpeed(Entity<BadBloodRallyHowlComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    #endregion

    #region Mend Beast

    private void OnMendBeastAction(Entity<BadBloodBeastMasterComponent> ent, ref BadBloodMendBeastActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanMend(ent, args.Target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), ent, ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.MendDelay, new BadBloodMendBeastDoAfterEvent(),
            ent.Owner, target: args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_packDoAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    /// <summary>
    /// Whether <paramref name="master"/> may mend <paramref name="beast"/> now: a living clan beast, not mended in the
    /// last <see cref="BadBloodBeastMasterComponent.MendCooldown"/>. <paramref name="reason"/> is a loc key.
    /// </summary>
    public bool CanMend(Entity<BadBloodBeastMasterComponent> master, EntityUid beast, out string reason)
    {
        reason = "cmu-bad-blood-beast-mend-not-beast";
        if (!TryComp(beast, out BadBloodBeastComponent? comp) || _mobState.IsDead(beast))
            return false;

        reason = "cmu-bad-blood-beast-mend-cooldown";
        return _timing.CurTime >= comp.NextMendAt;
    }

    private void OnMendBeastDoAfter(Entity<BadBloodBeastMasterComponent> ent, ref BadBloodMendBeastDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } beast)
            return;

        args.Handled = true;
        TryMend(ent, beast);
    }

    /// <summary>
    /// Heals <paramref name="beast"/> by <see cref="BadBloodBeastMasterComponent.MendHeal"/> and starts its cooldown.
    /// The do-after of the Mend Beast action ends here.
    /// </summary>
    public bool TryMend(Entity<BadBloodBeastMasterComponent> master, EntityUid beast)
    {
        if (!CanMend(master, beast, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), master, master, PopupType.SmallCaution);
            return false;
        }

        var comp = Comp<BadBloodBeastComponent>(beast);
        comp.NextMendAt = _timing.CurTime + master.Comp.MendCooldown;
        _packXeno.HealDamage(beast, master.Comp.MendHeal);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-beast-mended", ("beast", beast)), beast, master, PopupType.Medium);
        return true;
    }

    #endregion

    #region Pack Bond

    // the clan hound camera, built into the Beast-Master: one camera console parented to them
    private void OnPackSightAction(Entity<BadBloodBeastMasterComponent> ent, ref BadBloodPackSightActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.PackSightCamera is not { } camera || TerminatingOrDeleted(camera))
        {
            camera = Spawn(ent.Comp.PackSightCameraPrototype, new EntityCoordinates(ent.Owner, Vector2.Zero));
            ent.Comp.PackSightCamera = camera;
        }

        _packUi.TryOpenUi(camera, RMCCameraUiKey.Key, ent.Owner);
    }

    // once a second: bonded beasts near their Beast-Master regenerate BondRegenMultiplier times as fast
    private void UpdatePackBond()
    {
        var masters = EntityQueryEnumerator<BadBloodBeastMasterComponent>();
        while (masters.MoveNext(out var uid, out var master))
        {
            if (master.BondRegenMultiplier <= 1f || !IsLivingMaster(uid))
                continue;

            var here = _packTransform.GetMapCoordinates(uid);
            foreach (var beast in master.Beasts)
            {
                if (TerminatingOrDeleted(beast) ||
                    _mobState.IsDead(beast) ||
                    !InRange(beast, here, master.BondRadius) ||
                    !_packThresholds.TryGetIncapThreshold(beast, out var incap))
                {
                    continue;
                }

                var standing = (incap.Value.Float() / 65f + StandRegenFlat) * StandRegenShare;
                _packXeno.HealDamage(beast, standing * (master.BondRegenMultiplier - 1f));
            }
        }
    }

    #endregion

    #region War-paint

    private void OnWarPaintMeleeDamage(Entity<BadBloodWarPaintComponent> ent, ref GetMeleeDamageEvent args)
    {
        // a beast's bite is its own body; a war-painted kennel never swings
        if (args.Weapon != ent.Owner)
            return;

        args.Damage *= ent.Comp.Multiplier;
    }

    #endregion

    // Blood Scent and Rally Howl run out
    private void UpdatePackEffects(TimeSpan now)
    {
        _expiredScratch.Clear();
        var scents = EntityQueryEnumerator<BadBloodBloodScentComponent>();
        while (scents.MoveNext(out var uid, out var scent))
        {
            if (now >= scent.ExpiresAt)
                _expiredScratch.Add(uid);
        }

        foreach (var uid in _expiredScratch)
        {
            RemComp<BadBloodBloodScentComponent>(uid);
        }

        _expiredScratch.Clear();
        var rallies = EntityQueryEnumerator<BadBloodRallyHowlComponent>();
        while (rallies.MoveNext(out var uid, out var rally))
        {
            if (now >= rally.ExpiresAt)
                _expiredScratch.Add(uid);
        }

        foreach (var uid in _expiredScratch)
        {
            RemComp<BadBloodRallyHowlComponent>(uid);
            _speed.RefreshMovementSpeedModifiers(uid);
        }

        _expiredScratch.Clear();
    }

    private bool InRange(EntityUid beast, MapCoordinates here, float range)
    {
        var there = _packTransform.GetMapCoordinates(beast);
        return there.MapId == here.MapId && Vector2.Distance(there.Position, here.Position) <= range;
    }
}
