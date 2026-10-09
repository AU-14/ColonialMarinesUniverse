using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vehicle.Jeep;

/// <summary>
/// Builds up a vehicle's dirt as it drives, and splashes it with the blood of riders who are hurt or
/// bleeding and of anyone it runs down.
/// </summary>
public sealed class CMUVehicleGrimeBuildUpSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUVehicleGrimeSystem _grime = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly Color DefaultBlood = Color.FromHex("#800000");

    // Height of a surgical bed's mattress top in the art's voxels (jeep_pixel_art.py BED).
    private const float BedTop = 15f;

    // Mobs a vehicle has run down, until their run-over wears off, so each hit splashes once.
    private readonly Dictionary<EntityUid, TimeSpan> _runOver = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BodyPartDamagedEvent>(OnBodyPartDamaged);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var vehicles = EntityQueryEnumerator<CMUVehicleGrimeComponent, GridVehicleMoverComponent, TransformComponent>();
        while (vehicles.MoveNext(out var uid, out var grime, out var mover, out var xform))
        {
            var distance = MathF.Abs(mover.CurrentSpeed) * frameTime;
            if (distance > 0f && grime.RawDirt < 1f)
                _grime.SetDirt((uid, grime), grime.RawDirt + distance * grime.DirtPerTile * GroundFactor(grime, xform));

            if (now >= grime.NextDrip)
            {
                grime.NextDrip = now + grime.DripInterval;
                DripRiders(uid);
            }
        }

        var runOver = EntityQueryEnumerator<VehicleRunoverComponent>();
        while (runOver.MoveNext(out var mob, out var hit))
        {
            if (_runOver.TryGetValue(mob, out var until) && until == hit.ExpiresAt)
                continue;

            _runOver[mob] = hit.ExpiresAt;
            if (HasComp<CMUVehicleGrimeComponent>(hit.Vehicle) && BloodColor(mob) is { } color)
                SplashFront(hit.Vehicle, color);
        }

        foreach (var mob in _runOver.Keys.ToList())
        {
            if (!HasComp<VehicleRunoverComponent>(mob))
                _runOver.Remove(mob);
        }
    }

    /// <summary>
    /// Bare ground (diggable) dirties a vehicle fastest, paved ground outdoors less, floors indoors
    /// hardly at all.
    /// </summary>
    private float GroundFactor(CMUVehicleGrimeComponent grime, TransformComponent xform)
    {
        if (xform.GridUid is not { } grid ||
            !TryComp(grid, out MapGridComponent? gridComp) ||
            !_map.TryGetTileRef(grid, gridComp, xform.Coordinates, out var tile) ||
            _tiles[tile.Tile.TypeId] is not ContentTileDefinition def)
        {
            return grime.IndoorFactor;
        }

        if (def.CanDig)
            return 1f;

        return def.Weather ? grime.PavedFactor : grime.IndoorFactor;
    }

    private void OnBodyPartDamaged(ref BodyPartDamagedEvent args)
    {
        if (!TryGetSeat(args.Body, out var vehicle, out var seat) ||
            !TryComp(vehicle, out CMUVehicleGrimeComponent? grime) ||
            !_prototypes.TryIndex(BruteGroup, out var bruteGroup) ||
            !args.Delta.TryGetDamageInGroup(bruteGroup, out var brute) ||
            brute < grime.WoundSplashMinDamage ||
            !_random.Prob(Math.Clamp(0.3f + 0.04f * brute.Float(), 0f, 1f)) ||
            BloodColor(args.Body) is not { } color)
        {
            return;
        }

        SplashSeat(vehicle, seat, color, splat: true);
    }

    /// <summary>
    /// Each bleeding rider may drip on their seat, more likely the worse they bleed.
    /// </summary>
    private void DripRiders(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleSeatsComponent? seats))
            return;

        foreach (var seat in seats.SeatEntities)
        {
            if (!TryComp(seat, out StrapComponent? strap))
                continue;

            foreach (var rider in strap.BuckledEntities)
            {
                var bleeding = Bleeding(rider);
                if (bleeding > 0f && _random.Prob(Math.Clamp(0.25f + 0.2f * bleeding, 0f, 0.9f)) && BloodColor(rider) is { } color)
                    SplashSeat(vehicle, seat, color, splat: bleeding >= 3f);
            }
        }
    }

    /// <summary>
    /// How badly a mob bleeds: its bloodstream's bleed, or its worst wound's bleeding tier.
    /// </summary>
    private float Bleeding(EntityUid mob)
    {
        var bleeding = TryComp(mob, out BloodstreamComponent? blood) ? blood.BleedAmount : 0f;
        foreach (var (part, _) in _body.GetBodyChildren(mob))
        {
            if (TryComp(part, out BodyPartWoundComponent? wound))
                bleeding = MathF.Max(bleeding, (float) wound.ExternalBleeding);
        }

        return bleeding;
    }

    private Color? BloodColor(EntityUid mob)
    {
        if (!TryComp(mob, out BloodstreamComponent? blood))
            return null;

        return _bloodstream.TryGetPrimaryReferenceReagent((mob, blood), out var reagent) &&
               _reagents.TryIndex(reagent, out var proto)
            ? proto.SubstanceColor
            : DefaultBlood;
    }

    private bool TryGetSeat(EntityUid rider, out EntityUid vehicle, out EntityUid seat)
    {
        vehicle = default;
        seat = default;
        if (!TryComp(rider, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } strap ||
            !TryComp(strap, out CMUVehicleSeatComponent? seatComp) ||
            seatComp.Vehicle is not { } driven)
        {
            return false;
        }

        vehicle = driven;
        seat = strap;
        return true;
    }

    /// <summary>
    /// Blood on a seat's cushion, or on the floor in front of it, or soaking a bed's mattress. Seat
    /// entities sit at their seat's offset in vehicle space (facing south), which is (-r, -f) / 32 in
    /// the art's voxels.
    /// </summary>
    private void SplashSeat(EntityUid vehicle, EntityUid seat, Color color, bool splat)
    {
        var local = Transform(seat).LocalPosition * 32f;
        TryComp(seat, out CMUVehicleSeatComponent? comp);
        if (comp is { LyingAngles.Count: > 0 })
        {
            var onBed = new Vector3(-local.Y + _random.NextFloat(-4f, 4f), -local.X + _random.NextFloat(-11f, 11f), BedTop);
            _grime.AddBlood(vehicle, color, onBed, "up", splat);
            return;
        }

        var f = -local.Y + _random.NextFloat(-3f, 3f);
        var r = -local.X + _random.NextFloat(-3f, 3f);
        var gunner = comp is { Lift: > 0f };
        var rear = local.Y > 16f;
        // The floor in front of a seat turned round to face the back is behind it.
        var ahead = comp is { Reversed: true } ? -6f : 6f;
        Vector3 anchor;
        if (gunner || _random.Prob(0.35f))
            anchor = new Vector3(f + (gunner ? 0f : ahead), r, gunner ? 8f : 7f);
        else
            anchor = new Vector3(f, r, rear ? 16f : 13f);

        _grime.AddBlood(vehicle, color, anchor, "up", splat);
    }

    /// <summary>
    /// Blood thrown up the front: the bumper, the grille or the hood.
    /// </summary>
    private void SplashFront(EntityUid vehicle, Color color)
    {
        for (var i = _random.Next(1, 3); i > 0; i--)
        {
            switch (_random.Next(3))
            {
                case 0:
                    _grime.AddBlood(vehicle, color, new Vector3(31f, _random.Next(-17, 17), 4f), "+f", true);
                    break;
                case 1:
                    _grime.AddBlood(vehicle, color, new Vector3(31f, _random.Next(-7, 7), _random.Next(9, 17)), "+f", true);
                    break;
                default:
                    _grime.AddBlood(vehicle, color, new Vector3(_random.Next(18, 28), _random.Next(-6, 6), 20f), "up", true);
                    break;
            }
        }
    }
}
