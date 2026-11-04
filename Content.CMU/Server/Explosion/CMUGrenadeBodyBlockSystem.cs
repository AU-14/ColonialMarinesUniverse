using Content.Server.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Stun;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Explosion;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Server.Administration.Logs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Explosion;

public sealed partial class CMUGrenadeBodyBlockSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float BlockRange = 0.5f;
    private const int LimbsLost = 2;
    private const float DecapitationChance = 0.1f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGrenadeForcedHitComponent, HitLocationResolveEvent>(OnForcedHitResolve);
    }

    private static readonly CMUMedicalBodyPartKey[] Limbs =
    {
        new(BodyPartType.Arm, BodyPartSymmetry.Left),
        new(BodyPartType.Arm, BodyPartSymmetry.Right),
        new(BodyPartType.Leg, BodyPartSymmetry.Left),
        new(BodyPartType.Leg, BodyPartSymmetry.Right),
    };

    public bool TryGetBlocker(EntityUid grenade, out EntityUid blocker)
    {
        blocker = default;
        if (TryComp<CMUGrenadeBlockedComponent>(grenade, out var blocked))
        {
            if (blocked.Blocker is not { } cached || TerminatingOrDeleted(cached))
                return false;

            blocker = cached;
            return true;
        }

        if (!HasComp<ItemComponent>(grenade) ||
            HasComp<ProjectileComponent>(grenade) ||
            _container.IsEntityInContainer(grenade))
        {
            return false;
        }

        var coords = _transform.GetMapCoordinates(grenade);
        var bestDistance = float.MaxValue;
        EntityUid? best = null;
        foreach (var candidate in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, BlockRange))
        {
            if (!IsDeliberateBlocker(candidate))
                continue;

            var distance = (_transform.GetMapCoordinates(candidate).Position - coords.Position).Length();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate;
        }

        EnsureComp<CMUGrenadeBlockedComponent>(grenade).Blocker = best;
        if (best is not { } found)
            return false;

        blocker = found;
        _popup.PopupEntity(Loc.GetString("cmu-grenade-body-block", ("blocker", found)),
            found,
            Filter.Pvs(found),
            true,
            PopupType.LargeCaution);
        _adminLog.Add(LogType.Explosion, LogImpact.Medium,
            $"{ToPrettyString(found):player} smothered {ToPrettyString(grenade):grenade} with their body");
        return true;
    }

    private bool IsDeliberateBlocker(EntityUid uid)
    {
        return _mobState.IsAlive(uid) &&
               !HasComp<RMCUnconsciousComponent>(uid) &&
               _standing.IsDown(uid) &&
               TryComp<RMCRestComponent>(uid, out var rest) &&
               rest.Resting;
    }

    public bool TryAbsorbBlast(EntityUid grenade, ProtoId<ExplosionPrototype> explosionType, float totalIntensity, float slope, float maxIntensity)
    {
        if (!TryGetBlocker(grenade, out var blocker))
            return false;

        if (!_proto.TryIndex(explosionType, out var type))
            return true;

        var radius = _explosion.IntensityToRadius(totalIntensity, slope, maxIntensity);
        var epicenter = MathF.Min(maxIntensity, slope * MathF.Max(radius, 1f));
        ApplyToEachLimb(blocker, type.DamagePerIntensity * epicenter, grenade);
        _audio.PlayPvs(type.SmallSound, blocker);
        KnockDownBystanders(grenade, blocker, type, epicenter, slope, radius);
        ApplyBlockInjuries(grenade, blocker);
        return true;
    }

    public bool TryAbsorbShrapnel(EntityUid grenade, ProjectileGrenadeComponent fragments)
    {
        if (!TryGetBlocker(grenade, out var blocker))
            return false;

        var total = new DamageSpecifier();
        if (fragments.FillPrototype is { } fill &&
            _proto.TryIndex(fill, out var fillProto) &&
            fillProto.TryComp<ProjectileComponent>(out var fillProjectile, _factory))
        {
            for (var i = 0; i < fragments.UnspawnedCount; i++)
                total += fillProjectile.Damage;
        }

        foreach (var contained in fragments.Container.ContainedEntities)
        {
            if (TryComp<ProjectileComponent>(contained, out var projectile))
                total += projectile.Damage;
        }

        if (!total.Empty)
            ApplyToEachLimb(blocker, total, grenade);

        ApplyBlockInjuries(grenade, blocker);
        return true;
    }

    private void KnockDownBystanders(EntityUid grenade, EntityUid blocker, ExplosionPrototype type, float epicenter, float slope, float radius)
    {
        var origin = _transform.GetMapCoordinates(grenade);
        var range = MathF.Max(radius, 1f) + 1f;
        foreach (var bystander in _lookup.GetEntitiesInRange<StunOnExplosionReceivedComponent>(origin, range))
        {
            if (bystander.Owner == blocker)
                continue;

            var target = _transform.GetMapCoordinates(bystander);
            var distance = (target.Position - origin.Position).Length();
            var intensity = epicenter - slope * MathF.Floor(distance);
            if (intensity <= 0 || !_interaction.InRangeUnobstructed(origin, target, range))
                continue;

            _rmcExplosion.ApplyExplosionStunOnly(bystander, type.ID, origin, type.DamagePerIntensity * intensity);
        }
    }

    private void ApplyToEachLimb(EntityUid blocker, DamageSpecifier damage, EntityUid grenade)
    {
        foreach (var limb in Limbs)
        {
            if (!_medicalIndex.TryGetBodyPart(blocker, limb, out var part))
                continue;

            var forced = EnsureComp<CMUGrenadeForcedHitComponent>(blocker);
            forced.Part = part;
            forced.Type = limb.Type;
            _damageable.TryChangeDamage(blocker, damage, origin: grenade);
        }

        RemComp<CMUGrenadeForcedHitComponent>(blocker);
    }

    private void OnForcedHitResolve(Entity<CMUGrenadeForcedHitComponent> ent, ref HitLocationResolveEvent args)
    {
        if (ent.Comp.Part is not { } part)
            return;

        args.ResolvedPart = ent.Comp.Type;
        args.ResolvedPartEntity = part;
        args.Handled = true;
    }

    private void ApplyBlockInjuries(EntityUid grenade, EntityUid blocker)
    {
        if (!TryComp<CMUGrenadeBlockedComponent>(grenade, out var blocked) || blocked.InjuriesApplied)
            return;

        blocked.InjuriesApplied = true;

        var candidates = new List<CMUMedicalBodyPartKey>();
        foreach (var limb in Limbs)
        {
            if (_medicalIndex.TryGetBodyPart(blocker, limb, out _))
                candidates.Add(limb);
        }

        _random.Shuffle(candidates);
        var severed = 0;
        foreach (var limb in candidates)
        {
            if (severed >= LimbsLost)
                break;

            if (TrySeverPart(blocker, limb))
                severed++;
        }

        if (_random.Prob(DecapitationChance))
            TrySeverPart(blocker, new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None));
    }

    private bool TrySeverPart(EntityUid body, CMUMedicalBodyPartKey key)
    {
        if (!_medicalIndex.TryGetBodyPart(body, key, out var part))
            return false;

        var ev = new BodyPartSeverAttemptEvent(body, part, key.Type);
        RaiseLocalEvent(part, ref ev, broadcast: true);
        return ev.Succeeded;
    }

    public void TrySeverHoldingHand(EntityUid grenade)
    {
        if (!_container.TryGetContainingContainer((grenade, null, null), out var container) ||
            !TryComp<HandsComponent>(container.Owner, out var hands) ||
            !_hands.TryGetHand((container.Owner, hands), container.ID, out var hand))
        {
            return;
        }

        var symmetry = hand.Value.Location switch
        {
            HandLocation.Left => BodyPartSymmetry.Left,
            HandLocation.Right => BodyPartSymmetry.Right,
            _ => (BodyPartSymmetry?) null,
        };

        var body = container.Owner;
        if (symmetry is not { } side)
            return;

        if (!TrySeverPart(body, new CMUMedicalBodyPartKey(BodyPartType.Hand, side)))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-grenade-hand-severed", ("victim", body)),
            body,
            Filter.Pvs(body),
            true,
            PopupType.LargeCaution);
        _adminLog.Add(LogType.Explosion, LogImpact.Medium,
            $"{ToPrettyString(body):player} lost a hand to {ToPrettyString(grenade):grenade} detonating in it");
    }
}

[RegisterComponent]
public sealed partial class CMUGrenadeBlockedComponent : Component
{
    [DataField]
    public EntityUid? Blocker;

    [DataField]
    public bool InjuriesApplied;
}

[RegisterComponent]
public sealed partial class CMUGrenadeForcedHitComponent : Component
{
    [DataField]
    public EntityUid? Part;

    [DataField]
    public BodyPartType Type;
}
