using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Slow;
using Content.Shared.Actions;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-702 / ECS-712 (D17): the front-line Medic, server half.
/// <list type="bullet">
/// <item>Field Mend (<see cref="BadBloodFieldMendComponent"/>): a 3 s do-after on a Bad Blood or clan beast that stops
/// surface bleeding on every part, splints the worst fracture, lowers the pain a tier and clears slows. Each medical
/// step is skipped while its CMU layer is off. Internal bleeding and organs are left to the medicomp.</item>
/// <item>Blood Rite (<see cref="BadBloodBloodRiteComponent"/>): a 6 s do-after on a critical Bad Blood or clan beast.
/// Heals it to just under its crit threshold, stops surface bleeding, gives a short second wind and rite
/// sickness.</item>
/// <item>Death's Door: a critical Bad Blood near a living Medic takes half its asphyxiation and blood-loss damage.
/// A critical human body has no crit damage here (DamageMobState is off on the species base); it drains by
/// suffocating (RespiratorSystem: no breathing while incapacitated), bleeding, and being dragged.</item>
/// <item>The Medic's armour counts for less.</item>
/// </list>
/// The field kit's quicker use and second wind's speed are predicted in <see cref="BadBloodMedicKitSystem"/>.
/// </summary>
public sealed class BadBloodMedicSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype>[] DeathsDoorTypes = ["Asphyxiation", "Bloodloss"];
    private static readonly TimeSpan DeathsDoorInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private SharedCMUSplintItemSystem _splints = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;

    private TimeSpan _nextDeathsDoor;
    private readonly List<(MapCoordinates Where, float Range, float Multiplier)> _medics = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodFieldMendComponent, ComponentStartup>(OnMendStartup);
        SubscribeLocalEvent<BadBloodFieldMendComponent, ComponentShutdown>(OnMendShutdown);
        SubscribeLocalEvent<BadBloodFieldMendComponent, BadBloodFieldMendActionEvent>(OnMendAction);
        SubscribeLocalEvent<BadBloodFieldMendComponent, BadBloodFieldMendDoAfterEvent>(OnMendDoAfter);
        SubscribeLocalEvent<BadBloodFieldMendComponent, CMGetArmorEvent>(OnGetArmor);

        SubscribeLocalEvent<BadBloodBloodRiteComponent, ComponentStartup>(OnRiteStartup);
        SubscribeLocalEvent<BadBloodBloodRiteComponent, ComponentShutdown>(OnRiteShutdown);
        SubscribeLocalEvent<BadBloodBloodRiteComponent, BadBloodBloodRiteActionEvent>(OnRiteAction);
        SubscribeLocalEvent<BadBloodBloodRiteComponent, BadBloodBloodRiteDoAfterEvent>(OnRiteDoAfter);

        SubscribeLocalEvent<BadBloodDeathsDoorComponent, DamageModifyAfterResistEvent>(OnDeathsDoorDamage);
    }

    /// <summary>
    /// A clan body the Medic may treat: a Bad Blood or a clan beast.
    /// </summary>
    public bool IsClanPatient(EntityUid uid)
    {
        return HasComp<YautjaBadBloodComponent>(uid) || HasComp<BadBloodBeastComponent>(uid);
    }

    #region Field Mend

    private void OnMendStartup(Entity<BadBloodFieldMendComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.MendActionEntity, ent.Comp.MendAction);
        Dirty(ent);
    }

    private void OnMendShutdown(Entity<BadBloodFieldMendComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _actions.RemoveAction(ent.Owner, ent.Comp.MendActionEntity);
    }

    private void OnGetArmor(Entity<BadBloodFieldMendComponent> ent, ref CMGetArmorEvent args)
    {
        args.ArmorModifier *= ent.Comp.ArmorMultiplier;
    }

    private void OnMendAction(Entity<BadBloodFieldMendComponent> ent, ref BadBloodFieldMendActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartFieldMend(ent, args.Target))
            args.Handled = true;
    }

    /// <summary>
    /// Starts a Field Mend on <paramref name="target"/>, telling the Medic why not when it is refused.
    /// </summary>
    public bool TryStartFieldMend(Entity<BadBloodFieldMendComponent> medic, EntityUid target)
    {
        if (!CanFieldMend(medic, target, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, medic, medic, PopupType.SmallCaution);

            return false;
        }

        if (!_power.HasPowerPopup(medic, medic.Comp.MendCost, popupOnServer: true))
            return false;

        var doAfter = new DoAfterArgs(EntityManager,
            medic.Owner,
            medic.Comp.MendDelay,
            new BadBloodFieldMendDoAfterEvent(),
            medic.Owner,
            target: target)
        {
            // only the Medic stopping counts: the patient may keep moving while it is patched
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = false,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            DistanceThreshold = 2f,
            BlockDuplicate = true,
            TargetEffect = "RMCEffectHealBusy",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-medic-mend-start", ("target", target)), medic, medic);
        return true;
    }

    private bool CanFieldMend(Entity<BadBloodFieldMendComponent> medic, EntityUid target, out string? reason)
    {
        reason = null;
        if (!IsClanPatient(target) || _mobState.IsDead(target))
        {
            reason = Loc.GetString("cmu-bad-blood-medic-not-clan");
            return false;
        }

        if (TryComp(target, out BadBloodFieldMendedComponent? mended) && _timing.CurTime < mended.NextMend)
        {
            var seconds = (int) Math.Ceiling((mended.NextMend - _timing.CurTime).TotalSeconds);
            reason = Loc.GetString("cmu-bad-blood-medic-mend-cooldown", ("target", target), ("seconds", seconds));
            return false;
        }

        return true;
    }

    private void OnMendDoAfter(Entity<BadBloodFieldMendComponent> ent, ref BadBloodFieldMendDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanFieldMend(ent, target, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, ent, ent, PopupType.SmallCaution);

            return;
        }

        if (!_power.TryRemovePower(ent, ent.Comp.MendCost))
            return;

        ApplyFieldMend(ent, target);
    }

    /// <summary>
    /// What a finished Field Mend does, cost and checks aside: stop surface bleeding on every part, splint the worst
    /// fracture, take the pain down a tier for a while, clear slows, and start the per-target cooldown.
    /// </summary>
    public void ApplyFieldMend(Entity<BadBloodFieldMendComponent> medic, EntityUid target)
    {
        if (HasComp<CMUHumanMedicalComponent>(target))
        {
            if (_wounds.IsEnabled())
            {
                foreach (var (part, _) in _medicalIndex.GetBodyParts(target))
                {
                    _wounds.StopSurfaceBleedingOnPart(part);
                }
            }

            if (_splints.IsLayerEnabled() && TryFindWorstFracture(target, out var fractured))
            {
                var splint = Spawn(medic.Comp.MendSplint, MapCoordinates.Nullspace);
                if (TryComp(splint, out CMUSplintItemComponent? splintItem))
                    _splints.ApplySplintToPart((splint, splintItem), fractured);

                QueueDel(splint);
            }

            if (_pain.IsLayerEnabled() && HasComp<PainShockComponent>(target))
            {
                _pain.AddPainSuppressionProfile(target,
                    medic.Comp.MendPainAccumulationSuppression,
                    medic.Comp.MendPainTiers,
                    0f,
                    medic.Comp.MendPainDuration);
            }
        }

        // the marines' slows and staggers
        RemComp<RMCSlowdownComponent>(target);
        RemComp<RMCSuperSlowdownComponent>(target);
        _movement.RefreshMovementSpeedModifiers(target);

        var mended = EnsureComp<BadBloodFieldMendedComponent>(target);
        mended.NextMend = _timing.CurTime + medic.Comp.MendTargetCooldown;

        _audio.PlayPvs(medic.Comp.MendSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-medic-mend-done", ("target", target)), medic, medic, PopupType.Medium);
        if (target != medic.Owner)
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-medic-mend-patient", ("medic", medic.Owner)), target, target, PopupType.Medium);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(medic.Owner):player} Field Mended Bad Blood patient {ToPrettyString(target):target}");
    }

    // the fracture a splint does the most for: the most severe one not already held at that severity
    private bool TryFindWorstFracture(EntityUid body, out EntityUid part)
    {
        part = default;
        var worst = FractureSeverity.None;
        foreach (var (id, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp(id, out FractureComponent? fracture) || fracture.Severity <= worst)
                continue;

            if (TryComp(id, out CMUSplintedComponent? splinted) && splinted.MaxSuppressed.IsAtLeast(fracture.Severity))
                continue;

            worst = fracture.Severity;
            part = id;
        }

        return worst != FractureSeverity.None;
    }

    #endregion

    #region Blood Rite

    private void OnRiteStartup(Entity<BadBloodBloodRiteComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.RiteActionEntity, ent.Comp.RiteAction);
        Dirty(ent);
    }

    private void OnRiteShutdown(Entity<BadBloodBloodRiteComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _actions.RemoveAction(ent.Owner, ent.Comp.RiteActionEntity);
    }

    private void OnRiteAction(Entity<BadBloodBloodRiteComponent> ent, ref BadBloodBloodRiteActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartBloodRite(ent, args.Target))
            args.Handled = true;
    }

    /// <summary>
    /// Starts a Blood Rite over a critical clan body, telling the Medic why not when it is refused.
    /// </summary>
    public bool TryStartBloodRite(Entity<BadBloodBloodRiteComponent> medic, EntityUid target)
    {
        if (!CanBloodRite(medic, target, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, medic, medic, PopupType.SmallCaution);

            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager,
            medic.Owner,
            medic.Comp.RiteDelay,
            new BadBloodBloodRiteDoAfterEvent(),
            medic.Owner,
            target: target)
        {
            // the Medic must stay still over the body
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = false,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            DistanceThreshold = 2f,
            BlockDuplicate = true,
            ForceVisible = true,
            TargetEffect = "RMCEffectHealBusy",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        // D16: a sound and a text everyone nearby gets, not only a light
        _audio.PlayPvs(medic.Comp.RiteSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-medic-rite-start", ("medic", medic.Owner), ("target", target)),
            target,
            PopupType.MediumCaution);
        return true;
    }

    private bool CanBloodRite(Entity<BadBloodBloodRiteComponent> medic, EntityUid target, out string? reason)
    {
        reason = null;
        if (target == medic.Owner || !IsClanPatient(target))
        {
            reason = Loc.GetString("cmu-bad-blood-medic-not-clan");
            return false;
        }

        if (!_mobState.IsCritical(target))
        {
            reason = Loc.GetString(_mobState.IsDead(target)
                ? "cmu-bad-blood-medic-rite-dead"
                : "cmu-bad-blood-medic-rite-not-critical", ("target", target));
            return false;
        }

        if (TryComp(target, out BadBloodRiteSicknessComponent? sickness) && _timing.CurTime < sickness.ExpiresAt)
        {
            var seconds = (int) Math.Ceiling((sickness.ExpiresAt - _timing.CurTime).TotalSeconds);
            reason = Loc.GetString("cmu-bad-blood-medic-rite-sick", ("target", target), ("seconds", seconds));
            return false;
        }

        return true;
    }

    private void OnRiteDoAfter(Entity<BadBloodBloodRiteComponent> ent, ref BadBloodBloodRiteDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanBloodRite(ent, target, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, ent, ent, PopupType.SmallCaution);

            return;
        }

        ApplyBloodRite(ent, target);
    }

    /// <summary>
    /// What a finished Blood Rite does, checks aside: heal to <see cref="BadBloodBloodRiteComponent.RiteMarginBelowCrit"/>
    /// under the crit threshold, stop surface bleeding, second wind, rite sickness and the green glow.
    /// </summary>
    public void ApplyBloodRite(Entity<BadBloodBloodRiteComponent> medic, EntityUid target)
    {
        var now = _timing.CurTime;
        if (_thresholds.TryGetThresholdForState(target, MobState.Critical, out var crit))
        {
            var goal = FixedPoint2.Max(FixedPoint2.Zero, crit.Value - medic.Comp.RiteMarginBelowCrit);
            var heal = _damageable.GetTotalDamage(target) - goal;
            if (heal > FixedPoint2.Zero)
                _damageable.HealEvenly(target, -heal, origin: medic.Owner);
        }

        if (_wounds.IsEnabled() && HasComp<CMUHumanMedicalComponent>(target))
        {
            foreach (var (part, _) in _medicalIndex.GetBodyParts(target))
            {
                _wounds.StopSurfaceBleedingOnPart(part);
            }
        }

        var hadWind = HasComp<BadBloodSecondWindComponent>(target);
        var wind = EnsureComp<BadBloodSecondWindComponent>(target);
        wind.ExpiresAt = now + medic.Comp.SecondWindDuration;
        wind.SpeedMultiplier = medic.Comp.SecondWindSpeedMultiplier;
        Dirty(target, wind);
        if (hadWind)
            _movement.RefreshMovementSpeedModifiers(target);

        EnsureComp<BadBloodRiteSicknessComponent>(target).ExpiresAt = now + medic.Comp.RiteSicknessDuration;

        Spawn(medic.Comp.RiteGlow, new EntityCoordinates(target, Vector2.Zero));
        _audio.PlayPvs(medic.Comp.RiteSound, target);

        var minutes = (int) Math.Ceiling(medic.Comp.RiteSicknessDuration.TotalMinutes);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-medic-rite-patient", ("minutes", minutes)), target, target, PopupType.Large);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-medic-rite-done", ("target", target)), medic, medic, PopupType.Medium);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(medic.Owner):player} performed a Bad Blood Blood Rite on {ToPrettyString(target):target}");
    }

    #endregion

    #region Death's Door

    private void OnDeathsDoorDamage(Entity<BadBloodDeathsDoorComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        if (!_mobState.IsCritical(ent))
            return;

        DamageSpecifier? scaled = null;
        foreach (var type in DeathsDoorTypes)
        {
            if (!args.Damage.DamageDict.TryGetValue(type, out var amount) || amount <= FixedPoint2.Zero)
                continue;

            // the specifier can be a component's own (the respirator's suffocation damage), so never edit it in place
            scaled ??= new DamageSpecifier(args.Damage);
            scaled.DamageDict[type] = amount * ent.Comp.Multiplier;
        }

        if (scaled != null)
            args.Damage = scaled;
    }

    /// <summary>
    /// Marks every critical Bad Blood within reach of a living Medic for Death's Door, and unmarks the rest.
    /// Runs on its own every half second.
    /// </summary>
    public void RefreshDeathsDoor()
    {
        _medics.Clear();
        var medics = EntityQueryEnumerator<BadBloodBloodRiteComponent, MobStateComponent, TransformComponent>();
        while (medics.MoveNext(out var uid, out var rite, out var state, out var xform))
        {
            if (_mobState.IsAlive(uid, state))
                _medics.Add((_transform.GetMapCoordinates(uid, xform), rite.DeathsDoorRange, rite.DeathsDoorMultiplier));
        }

        var clan = EntityQueryEnumerator<YautjaBadBloodComponent, MobStateComponent, TransformComponent>();
        while (clan.MoveNext(out var uid, out _, out var state, out var xform))
        {
            float? multiplier = null;
            if (_medics.Count > 0 && _mobState.IsCritical(uid, state))
            {
                var here = _transform.GetMapCoordinates(uid, xform);
                foreach (var (where, range, mult) in _medics)
                {
                    if (where.MapId != here.MapId || (where.Position - here.Position).LengthSquared() > range * range)
                        continue;

                    multiplier = multiplier is { } best ? MathF.Min(best, mult) : mult;
                }
            }

            if (multiplier is { } found)
                EnsureComp<BadBloodDeathsDoorComponent>(uid).Multiplier = found;
            else if (HasComp<BadBloodDeathsDoorComponent>(uid))
                RemComp<BadBloodDeathsDoorComponent>(uid);
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var winds = EntityQueryEnumerator<BadBloodSecondWindComponent>();
        while (winds.MoveNext(out var uid, out var wind))
        {
            if (now >= wind.ExpiresAt)
            {
                RemCompDeferred<BadBloodSecondWindComponent>(uid);
                _popup.PopupEntity(Loc.GetString("cmu-bad-blood-medic-second-wind-end"), uid, uid, PopupType.SmallCaution);
            }
        }

        var sick = EntityQueryEnumerator<BadBloodRiteSicknessComponent>();
        while (sick.MoveNext(out var uid, out var sickness))
        {
            if (now >= sickness.ExpiresAt)
                RemCompDeferred<BadBloodRiteSicknessComponent>(uid);
        }

        var tonic = EntityQueryEnumerator<BadBloodTonicRegrowthComponent>();
        while (tonic.MoveNext(out var uid, out var regrowth))
        {
            if (now >= regrowth.ExpiresAt)
                RemCompDeferred<BadBloodTonicRegrowthComponent>(uid);
        }

        if (now < _nextDeathsDoor)
            return;

        _nextDeathsDoor = now + DeathsDoorInterval;
        RefreshDeathsDoor();
    }
}
