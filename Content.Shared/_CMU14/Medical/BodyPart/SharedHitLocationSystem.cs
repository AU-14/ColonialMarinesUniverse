using System.Collections.Generic;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CMU14.Medical.BodyPart;

/// <summary>
///     Subscribes to <see cref="BeforeDamageChangedEvent"/> (fires for every
///     incoming damage application, including explosions which use
///     <c>ignoreResistances: true</c> and skip <see cref="DamageModifyEvent"/>)
///     and stashes the resolution so <see cref="SharedBodyPartHealthSystem"/> can
///     deduct from the right part on the post-application
///     <see cref="DamageChangedEvent"/>.
/// </summary>
public abstract class SharedHitLocationSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedBodyZoneTargetingSystem ZoneTargeting = default!;
    [Dependency] protected readonly SharedTransformSystem Transform = default!;
    [Dependency] protected readonly SkillsSystem Skills = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private readonly Dictionary<EntityUid, HitLocationResolveEvent> _pendingHits = new();
    private readonly Dictionary<EntityUid, int> _bodyZoneSuppressedOrigins = new();

    private bool _medicalEnabled;
    private bool _hitLocationEnabled;
    private float _headWeight;
    private float _chestWeight;
    private float _armWeight;
    private float _legWeight;

    public bool TryConsumePendingHit(EntityUid target, out HitLocationResolveEvent hit)
        => _pendingHits.Remove(target, out hit);

    public BodyZoneTargetingSuppression SuppressBodyZoneTargeting(EntityUid origin)
    {
        _bodyZoneSuppressedOrigins.TryGetValue(origin, out var depth);
        _bodyZoneSuppressedOrigins[origin] = depth + 1;
        return new BodyZoneTargetingSuppression(this, origin);
    }

    /// <summary>
    ///     Sets the next-hit forced zone on <paramref name="target"/>. The override
    ///     is single-shot — cleared after the next damage event.
    /// </summary>
    public void SetForcedHit(Entity<HitLocationComponent?> target, BodyPartType? part)
    {
        if (!Resolve(target.Owner, ref target.Comp, logMissing: false))
            return;
        target.Comp.NextHitOverride = part;
        Dirty(target.Owner, target.Comp);
    }

    /// <summary>
    ///     Defensive sweep for entities that no longer exist or whose stash was
    ///     never consumed (the matching <c>DamageChangedEvent</c> was suppressed).
    /// </summary>
    public void SweepStaleHits(EntityUid uid)
    {
        if (!Exists(uid))
            _pendingHits.Remove(uid);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitLocationComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.HitLocationEnabled, v => _hitLocationEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.HitLocationHeadWeight, v => _headWeight = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.HitLocationChestWeight, v => _chestWeight = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.HitLocationArmWeight, v => _armWeight = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.HitLocationLegWeight, v => _legWeight = v, true);
    }

    private void OnBeforeDamageChanged(Entity<HitLocationComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!_medicalEnabled || !_hitLocationEnabled)
            return;

        if (!HasComp<CMUHumanMedicalComponent>(ent))
            return;

        if (args.Damage.GetTotal() <= 0)
            return;

        if (!HasLocalizableDamage(args.Damage))
            return;

        var suppressBodyZone = args.Origin is { } origin && IsBodyZoneTargetingSuppressed(origin);
        var (forced, forcedSymmetry) = ResolveForcedSource(ent, args.Origin, suppressBodyZone);

        var resolve = new HitLocationResolveEvent(ent, args.Origin, args.Damage, forced, forcedSymmetry);
        RaiseLocalEvent(ent, ref resolve);

        if (!resolve.Handled)
            ResolveRandomly(ent, ref resolve, suppressBodyZone);

        if (resolve.Handled)
        {
            _pendingHits[ent.Owner] = resolve;
            var resolved = new HitLocationResolvedEvent(
                ent, args.Origin, resolve.ResolvedPart, resolve.ResolvedPartEntity);
            RaiseLocalEvent(ent, ref resolved);
        }

        ent.Comp.NextHitOverride = null;
        Dirty(ent);
    }

    private (BodyPartType? Forced, BodyPartSymmetry? Symmetry) ResolveForcedSource(
        Entity<HitLocationComponent> target,
        EntityUid? attacker,
        bool suppressBodyZone)
    {
        if (target.Comp.NextHitOverride is { } sentinel)
            return (sentinel, null);

        if (suppressBodyZone)
            return (null, null);

        if (attacker is { } a && ZoneTargeting.TryGetFreshSelection(a) is { } zone)
        {
            var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(zone);
            return (partType, symmetry);
        }

        return (null, null);
    }

    private void ResolveRandomly(
        Entity<HitLocationComponent> ent,
        ref HitLocationResolveEvent args,
        bool suppressBodyZone)
    {
        if (args.Forced is { } forced)
        {
            if (RollAimAccuracy(ent.Owner, args.Attacker, forced))
            {
                args.ResolvedPart = forced;
                args.ResolvedPartEntity =
                    FindFirstPartOfType(ent.Owner, forced, args.ForcedSymmetry)
                    ?? FindFirstPartOfType(ent.Owner, forced);
                args.Handled = true;
                return;
            }

            if (TryResolveCalledShotMiss(ent, ref args, forced))
                return;
        }

        ResolveFromWeights(ent, ref args, suppressBodyZone ? ReadAreaDamageWeights() : ReadWeights());
    }

    private bool TryResolveCalledShotMiss(
        Entity<HitLocationComponent> ent,
        ref HitLocationResolveEvent args,
        BodyPartType forced)
    {
        var weights = forced switch
        {
            BodyPartType.Head => new PartWeights(0f, _chestWeight, _armWeight, _legWeight),
            BodyPartType.Torso => new PartWeights(0f, _chestWeight * 0.35f, _armWeight, _legWeight),
            _ => default,
        };

        if (weights.Total <= 0f)
            return false;

        ResolveFromWeights(ent, ref args, weights);
        return true;
    }

    private void ResolveFromWeights(
        Entity<HitLocationComponent> ent,
        ref HitLocationResolveEvent args,
        PartWeights weights)
    {
        var roll = Random.NextFloat() * weights.Total;
        var partType = weights.Pick(roll);

        args.ResolvedPart = partType;
        args.ResolvedPartEntity = FindFirstPartOfType(ent.Owner, partType) ?? FindFirstPartOfType(ent.Owner, BodyPartType.Torso);
        args.Handled = true;
    }

    private bool RollAimAccuracy(EntityUid target, EntityUid? attacker, BodyPartType forced)
    {
        if (attacker is not { } a)
            return true;

        if (!TryComp<BodyZoneTargetingComponent>(a, out var aim))
            return true;

        var accuracy = aim.MeleeAccuracy;

        var atkXform = Transform(a);
        var tgtXform = Transform(target);
        if (atkXform.MapID == tgtXform.MapID && atkXform.MapID != Robust.Shared.Map.MapId.Nullspace)
        {
            var distance = (Transform.GetWorldPosition(atkXform) - Transform.GetWorldPosition(tgtXform)).Length();
            if (distance > aim.MeleeRangeTiles)
            {
                var skill = Skills.GetSkill(a, aim.RangedSkill);
                accuracy = aim.RangedBaseAccuracy + skill * aim.RangedSkillBonus;
            }
        }

        accuracy *= forced switch
        {
            BodyPartType.Head => aim.HeadAccuracyMultiplier,
            BodyPartType.Torso => aim.TorsoAccuracyMultiplier,
            _ => 1f,
        };
        accuracy = Math.Clamp(accuracy, 0f, 0.95f);
        return Random.NextFloat() <= accuracy;
    }

    private EntityUid? FindFirstPartOfType(EntityUid bodyId, BodyPartType type, BodyPartSymmetry? symmetry = null)
    {
        foreach (var (uid, partComp) in Body.GetBodyChildren(bodyId))
        {
            if (partComp.PartType != type)
                continue;
            if (symmetry is { } s && partComp.Symmetry != s)
                continue;
            return uid;
        }
        return null;
    }

    private bool HasLocalizableDamage(DamageSpecifier damage)
        => HasPositiveInGroup(damage, BruteGroup) || HasPositiveInGroup(damage, BurnGroup);

    private bool HasPositiveInGroup(DamageSpecifier damage, ProtoId<DamageGroupPrototype> groupId)
    {
        if (!_prototypes.TryIndex(groupId, out var group))
            return false;

        foreach (var type in group.DamageTypes)
        {
            if (damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                return true;
        }
        return false;
    }

    private PartWeights ReadWeights() => new(
        Head: _headWeight,
        Chest: _chestWeight,
        Arm: _armWeight,
        Leg: _legWeight);

    private PartWeights ReadAreaDamageWeights() => new(
        Head: _headWeight * 0.25f,
        Chest: _chestWeight * 0.60f,
        Arm: _armWeight * 1.40f,
        Leg: _legWeight * 1.40f);

    private bool IsBodyZoneTargetingSuppressed(EntityUid origin)
        => _bodyZoneSuppressedOrigins.ContainsKey(origin);

    private void UnsuppressBodyZoneTargeting(EntityUid origin)
    {
        if (!_bodyZoneSuppressedOrigins.TryGetValue(origin, out var depth))
            return;

        if (depth <= 1)
        {
            _bodyZoneSuppressedOrigins.Remove(origin);
            return;
        }

        _bodyZoneSuppressedOrigins[origin] = depth - 1;
    }

    public readonly struct BodyZoneTargetingSuppression : IDisposable
    {
        private readonly SharedHitLocationSystem? _system;
        private readonly EntityUid _origin;

        public BodyZoneTargetingSuppression(SharedHitLocationSystem system, EntityUid origin)
        {
            _system = system;
            _origin = origin;
        }

        public void Dispose()
        {
            _system?.UnsuppressBodyZoneTargeting(_origin);
        }
    }

    private readonly record struct PartWeights(float Head, float Chest, float Arm, float Leg)
    {
        public float Total => Head + Chest + Arm * 2f + Leg * 2f;

        public BodyPartType Pick(float roll)
        {
            if ((roll -= Head) < 0) return BodyPartType.Head;
            if ((roll -= Chest) < 0) return BodyPartType.Torso;
            if ((roll -= Arm) < 0) return BodyPartType.Arm;
            if ((roll -= Arm) < 0) return BodyPartType.Arm;
            if ((roll -= Leg) < 0) return BodyPartType.Leg;
            return BodyPartType.Leg;
        }
    }
}
