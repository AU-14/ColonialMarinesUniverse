using System.Numerics;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffect;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Lets an open vehicle's shut doors and raised windshield stop shots at its riders, and blows grit
/// into the eyes of the riders behind a lowered windshield.
/// </summary>
public sealed class CMUVehicleCoverSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private CMUJeepSystem _jeep = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUVehicleTyreSystem _tyres = default!;

    private const string Blinded = "Blinded";
    private static readonly string[] EyeSlots = ["eyes", "head", "mask"];

    private readonly List<(EntityUid Vehicle, DamageSpecifier Damage, EntityUid? Origin, EntityUid? Source)> _doorHits = new();
    private readonly List<(EntityUid Vehicle, float Wear)> _windshieldHits = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSeatedComponent, BeforeDamageChangedEvent>(OnBeforeDamage,
            after: [typeof(SharedHitLocationSystem)]);
    }

    private void OnBeforeDamage(Entity<CMUVehicleSeatedComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled ||
            args.Impact.Delivery != DamageImpactDelivery.Projectile ||
            CompOrNull<BuckleComponent>(ent)?.BuckledTo is not { } seatUid ||
            !TryComp(seatUid, out CMUVehicleSeatComponent? seat) ||
            seat.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUVehicleCoverComponent? cover) ||
            !TryComp(vehicle, out CMUJeepComponent? jeep) ||
            IsAboard(args.Origin, vehicle) ||
            ShotDirection(vehicle, args.Origin, args.Source) is not { } shot)
        {
            return;
        }

        var reach = -MathF.Cos(MathHelper.DegreesToRadians(cover.CoverAngle));
        if (seat.DoorSide is { } side &&
            seat.Door is { } door &&
            BelowChest(args) &&
            Vector2.Dot(shot, side.ToVec()) <= reach &&
            !CMUJeepSystem.GetPanelFlag(jeep, door) &&
            _jeep.IsPanelFitted(vehicle, jeep, door) &&
            !_jeep.IsHullWrecked(vehicle))
        {
            args.Cancelled = true;
            if (_net.IsServer)
            {
                _doorHits.Add((vehicle, args.Damage * cover.DoorDamage, args.Origin, args.Source));
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-cover-door"), ent, ent, PopupType.SmallCaution);
            }

            return;
        }

        if (seat.Windshield &&
            HeadOrChest(args) &&
            Vector2.Dot(shot, Direction.South.ToVec()) <= reach &&
            WindshieldWhole(vehicle, jeep) &&
            SharedRandomExtensions.PredictedProb(_timing, cover.WindshieldChance, GetNetEntity(ent), GetNetEntity(args.Source ?? args.Origin)))
        {
            args.Cancelled = true;
            if (_net.IsServer)
            {
                _windshieldHits.Add((vehicle, (float) args.Damage.GetTotal() * cover.WindshieldWear));
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-cover-windshield"), ent, ent, PopupType.SmallCaution);
            }
        }
    }

    /// <summary>
    /// Which way a shot is travelling in the vehicle's space, with the vehicle facing south: the
    /// bullet's own heading, or from the shooter to the vehicle.
    /// </summary>
    private Vector2? ShotDirection(EntityUid vehicle, EntityUid? origin, EntityUid? source)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(vehicle);
        var heading = Vector2.Zero;
        if (TryComp(source, out PhysicsComponent? physics))
            heading = physics.LinearVelocity;

        if (heading.LengthSquared() < 0.0001f && origin is { } shooter && !TerminatingOrDeleted(shooter))
            heading = position - _transform.GetWorldPosition(shooter);

        if (heading.LengthSquared() < 0.0001f)
            return null;

        return Vector2.Normalize((-rotation).RotateVec(heading));
    }

    private static bool BelowChest(BeforeDamageChangedEvent args)
    {
        return args.TargetZone is { } zone
            ? zone is TargetBodyZone.GroinPelvis or TargetBodyZone.LeftLeg or TargetBodyZone.RightLeg or
                TargetBodyZone.LeftFoot or TargetBodyZone.RightFoot
            : args.TargetPart is BodyPartType.Leg or BodyPartType.Foot;
    }

    private static bool HeadOrChest(BeforeDamageChangedEvent args)
    {
        return args.TargetZone is { } zone
            ? zone is TargetBodyZone.Head or TargetBodyZone.Chest
            : args.TargetPart is BodyPartType.Head or BodyPartType.Torso;
    }

    /// <summary>
    /// A raised, uncracked windshield, which stops shots.
    /// </summary>
    private bool WindshieldWhole(EntityUid vehicle, CMUJeepComponent jeep)
    {
        return WindshieldUp(vehicle, jeep) && !jeep.WindshieldDamaged;
    }

    /// <summary>
    /// A raised windshield, cracked or not, which keeps grit out.
    /// </summary>
    private bool WindshieldUp(EntityUid vehicle, CMUJeepComponent jeep)
    {
        return !jeep.WindshieldDown && _jeep.HasWindshield(vehicle, jeep);
    }

    private bool IsAboard(EntityUid? entity, EntityUid vehicle)
    {
        for (var i = 0; i < 5 && entity is { } current && current.IsValid(); i++)
        {
            if (current == vehicle)
                return true;

            if (!TryComp(current, out TransformComponent? xform))
                break;

            entity = xform.ParentUid;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        foreach (var (vehicle, damage, origin, source) in _doorHits)
        {
            if (!TerminatingOrDeleted(vehicle))
                _damageable.TryChangeDamage(vehicle, damage, origin: origin, tool: source);
        }

        _doorHits.Clear();

        foreach (var (vehicle, wear) in _windshieldHits)
        {
            if (TryComp(vehicle, out CMUJeepComponent? jeep))
                _jeep.WearWindshield((vehicle, jeep), wear);
        }

        _windshieldHits.Clear();

        var query = EntityQueryEnumerator<CMUVehicleCoverComponent, CMUJeepComponent, GridVehicleMoverComponent, CMUVehicleSeatsComponent>();
        while (query.MoveNext(out var uid, out var cover, out var jeep, out var mover, out var seats))
        {
            if (mover.MaxSpeed <= 0f ||
                MathF.Abs(mover.CurrentSpeed) < mover.MaxSpeed * cover.GritSpeed ||
                WindshieldUp(uid, jeep) ||
                !_tyres.OnRoughGround(uid))
            {
                continue;
            }

            foreach (var seatUid in seats.SeatEntities)
            {
                if (!TryComp(seatUid, out CMUVehicleSeatComponent? seat) ||
                    !seat.Windshield ||
                    !TryComp(seatUid, out StrapComponent? strap))
                {
                    continue;
                }

                foreach (var rider in strap.BuckledEntities)
                {
                    if (!_random.Prob(cover.GritChance * frameTime) ||
                        EyesCovered(rider) ||
                        !_status.TryAddStatusEffect<RMCBlindedComponent>(rider, Blinded, cover.GritDuration, false))
                    {
                        continue;
                    }

                    _popup.PopupEntity(Loc.GetString("cmu-vehicle-cover-grit"), rider, rider, PopupType.SmallCaution);
                }
            }
        }
    }

    /// <summary>
    /// Goggles, a visor or a mask that keeps grit out of the eyes.
    /// </summary>
    private bool EyesCovered(EntityUid rider)
    {
        foreach (var slot in EyeSlots)
        {
            if (_inventory.TryGetSlotEntity(rider, slot, out var worn) && HasComp<EyeProtectionComponent>(worn))
                return true;
        }

        return false;
    }
}
