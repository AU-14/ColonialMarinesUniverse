using System;
using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.ZLevels.Vehicles;

public sealed partial class CMUVehicleZTraversalSystem : EntitySystem
{
    private const float LandingCrushSampleRadius = 0.35f;
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleWheelSystem _wheels = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private readonly HashSet<EntityUid> _landingDamageTargets = new();
    private readonly HashSet<EntityUid> _interiorSoundRecipients = new();
    private readonly List<Vector2> _supportSamples = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUVehicleZTraversalComponent, CMUZLevelHitEvent>(OnVehicleZLevelHit);
    }

    private void OnVehicleZLevelHit(Entity<CMUVehicleZTraversalComponent> ent, ref CMUZLevelHitEvent args)
    {
        if (args.ImpactPower < ent.Comp.HardLandingMinVelocity)
            return;

        var damageType = _proto.Index(BluntDamageType);
        var baseDamage = MathF.Pow(args.ImpactPower, 2);

        PlayVehicleLandingSound(ent);

        if (ent.Comp.LandingWheelDamageMultiplier > 0f)
            _wheels.DamageWheels(ent.Owner, baseDamage * ent.Comp.LandingWheelDamageMultiplier);

        DamageVehicleOccupants(ent, damageType, baseDamage * ent.Comp.LandingOccupantDamageMultiplier);
        DamageFootprintTargets(ent, damageType, baseDamage * ent.Comp.LandingCrushDamageMultiplier);
    }

    private void DamageVehicleOccupants(
        Entity<CMUVehicleZTraversalComponent> ent,
        DamageTypePrototype damageType,
        float damageAmount)
    {
        if (damageAmount <= 0f ||
            !TryComp(ent.Owner, out VehicleInteriorComponent? interior))
        {
            return;
        }

        _landingDamageTargets.Clear();
        _landingDamageTargets.UnionWith(interior.Passengers);
        _landingDamageTargets.UnionWith(interior.Xenos);
        AddInteriorMapOccupants(ent.Owner, interior, _landingDamageTargets);

        foreach (var occupant in _landingDamageTargets)
        {
            if (TerminatingOrDeleted(occupant))
                continue;

            _damage.TryChangeDamage(occupant, new DamageSpecifier(damageType, damageAmount), true, origin: ent.Owner);
        }

        _landingDamageTargets.Clear();
    }

    private void AddInteriorMapOccupants(
        EntityUid vehicle,
        VehicleInteriorComponent interior,
        HashSet<EntityUid> recipients)
    {
        if (interior.MapId == MapId.Nullspace)
            return;

        var query = EntityQueryEnumerator<VehicleInteriorOccupantComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var occupant, out var xform))
        {
            if (occupant.Vehicle == vehicle &&
                xform.MapID == interior.MapId)
            {
                recipients.Add(uid);
            }
        }
    }

    private void PlayVehicleLandingSound(Entity<CMUVehicleZTraversalComponent> ent)
    {
        if (_net.IsClient ||
            !TryComp(ent.Owner, out VehicleSoundComponent? sound) ||
            sound.CollisionSound == null)
        {
            return;
        }

        var now = _timing.CurTime;
        if (sound.NextCollisionSound > now)
            return;

        _audio.PlayPvs(sound.CollisionSound, ent.Owner);
        PlayVehicleInteriorSound(ent.Owner, sound.CollisionSound);
        sound.NextCollisionSound = now + TimeSpan.FromSeconds(sound.CollisionSoundCooldown);
        Dirty(ent.Owner, sound);
    }

    private void PlayVehicleInteriorSound(EntityUid vehicle, SoundSpecifier sound)
    {
        if (!TryComp(vehicle, out VehicleInteriorComponent? interior))
            return;

        _interiorSoundRecipients.Clear();
        _interiorSoundRecipients.UnionWith(interior.Passengers);
        _interiorSoundRecipients.UnionWith(interior.Xenos);
        AddInteriorMapOccupants(vehicle, interior, _interiorSoundRecipients);

        var filter = Filter.Empty();
        foreach (var occupant in _interiorSoundRecipients)
        {
            AddInteriorSoundRecipient(filter, occupant);
        }

        if (filter.Count > 0)
            _audio.PlayGlobal(sound, filter, true);

        _interiorSoundRecipients.Clear();
    }

    private void AddInteriorSoundRecipient(Filter filter, EntityUid recipient)
    {
        if (TerminatingOrDeleted(recipient))
            return;

        if (TryComp(recipient, out ActorComponent? actor))
            filter.AddPlayer(actor.PlayerSession);
    }

    private void DamageFootprintTargets(
        Entity<CMUVehicleZTraversalComponent> ent,
        DamageTypePrototype damageType,
        float damageAmount,
        FixturesComponent? fixtures = null,
        TransformComponent? xform = null)
    {
        if (damageAmount <= 0f ||
            !Resolve(ent.Owner, ref fixtures, ref xform, false) ||
            xform.MapUid == null ||
            !CMUVehicleSupportFootprint.TryGetFixtureLocalAabb(fixtures, out var localBounds))
        {
            return;
        }

        CMUVehicleSupportFootprint.GenerateWorldSamples(
            localBounds,
            ent.Comp.SupportSampleSpacing,
            ent.Comp.SupportSampleInset,
            _transform.GetWorldPosition(xform),
            _transform.GetWorldRotation(xform),
            _supportSamples);

        _landingDamageTargets.Clear();
        foreach (var sample in _supportSamples)
        {
            _lookup.GetEntitiesInRange(
                xform.MapID,
                sample,
                LandingCrushSampleRadius,
                _landingDamageTargets,
                LookupFlags.Uncontained);
        }

        _landingDamageTargets.Remove(ent.Owner);
        foreach (var target in _landingDamageTargets)
        {
            if (TerminatingOrDeleted(target))
                continue;

            _damage.TryChangeDamage(target, new DamageSpecifier(damageType, damageAmount), origin: ent.Owner);
        }

        _landingDamageTargets.Clear();
    }

    public bool TryGetSupportState(
        Entity<CMUVehicleZTraversalComponent> ent,
        out CMUVehicleSupportState state,
        CMUZPhysicsComponent? zPhysics = null,
        FixturesComponent? fixtures = null,
        TransformComponent? xform = null)
    {
        state = default;
        if (!Resolve(ent.Owner, ref zPhysics, ref fixtures, ref xform, false))
            return false;

        if (!CMUVehicleSupportFootprint.TryGetFixtureLocalAabb(fixtures, out var localBounds))
            return false;

        CMUVehicleSupportFootprint.GenerateWorldSamples(
            localBounds,
            ent.Comp.SupportSampleSpacing,
            ent.Comp.SupportSampleInset,
            _transform.GetWorldPosition(xform),
            _transform.GetWorldRotation(xform),
            _supportSamples);

        var supported = 0;
        var supportedDistance = 0f;
        var stickyGround = false;
        Entity<CMUZPhysicsComponent?> zEnt = (ent.Owner, zPhysics);
        foreach (var sample in _supportSamples)
        {
            var distance = _zLevels.DistanceToGroundAtWorldPosition(zEnt, sample, out var sticky);
            if (!CMUVehicleSupportFootprint.IsSampleSupported(
                    distance,
                    sticky,
                    ent.Comp.MaxSupportStepHeight,
                    ent.Comp.SupportSnapDistance))
            {
                continue;
            }

            supported++;
            supportedDistance += distance;
            stickyGround |= sticky;
        }

        var averageDistance = supported > 0
            ? supportedDistance / supported
            : 0f;

        state = new CMUVehicleSupportState(
            supported,
            _supportSamples.Count,
            CMUVehicleSupportFootprint.GetUnsupportedFraction(supported, _supportSamples.Count),
            averageDistance,
            stickyGround);
        return true;
    }
}
