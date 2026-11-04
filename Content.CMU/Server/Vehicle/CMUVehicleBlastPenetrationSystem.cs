using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Explosion;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Vehicle;

/// <summary>
///     Replays explosions that strike a vehicle inside its interior map when the explosion type can
///     breach that vehicle's armour. See <see cref="CMUVehicleBlastPenetrationComponent"/>.
/// </summary>
public sealed class CMUVehicleBlastPenetrationSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleBlastPenetrationComponent, ExplosionReceivedEvent>(OnExplosionReceived);
    }

    private void OnExplosionReceived(Entity<CMUVehicleBlastPenetrationComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        // Own shells peppering the hull don't breach the firing crew's cabin, matching the operator exemption.
        if (args.SourceVehicle == ent.Owner)
            return;

        var intensity = GetIntensity(ent.Comp, args.Explosion);
        if (intensity <= 0f)
            return;

        // Only blasts centred on or right against the hull penetrate, not ones that merely clip the footprint.
        if (args.Epicenter.MapId != Transform(ent).MapID ||
            !_lookup.GetWorldAABB(ent).Enlarged(ent.Comp.RangeBuffer).Contains(args.Epicenter.Position))
        {
            return;
        }

        // No loaded interior means nothing inside to penetrate into.
        if (!TryComp(ent.Owner, out VehicleInteriorComponent? interior))
            return;

        var interiorGrid = interior.Grid;
        if (!interiorGrid.IsValid() || !TryComp(interiorGrid, out MapGridComponent? grid))
            return;

        _explosion.QueueExplosion(
            GetInteriorEpicenter((interiorGrid, grid), ent.Owner, args.Epicenter),
            args.Explosion,
            intensity,
            ent.Comp.Slope,
            ent.Comp.MaxTileIntensity,
            cause: ent.Owner,
            // The cabin itself is indestructible, so never break interior tiles or vent them to space.
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false);
    }

    public static float GetIntensity(CMUVehicleBlastPenetrationComponent comp, ProtoId<ExplosionPrototype> explosion)
    {
        if (comp.ExtraIntensities.TryGetValue(explosion, out var intensity) ||
            comp.Intensities.TryGetValue(explosion, out intensity))
        {
            return intensity;
        }

        return comp.DefaultIntensity;
    }

    private MapCoordinates GetInteriorEpicenter(Entity<MapGridComponent> grid, EntityUid vehicle, MapCoordinates exterior)
    {
        var bounds = grid.Comp.LocalAABB;
        var center = _transform.ToMapCoordinates(new EntityCoordinates(grid.Owner, bounds.Center));
        var offset = exterior.Position - _transform.GetWorldPosition(vehicle);
        if (offset.LengthSquared() < 0.25f)
            return center;

        // Nudge the interior blast towards the struck side so penetrations feel directional in the cabin.
        var local = (-_transform.GetWorldRotation(vehicle)).RotateVec(offset);
        return new MapCoordinates(center.Position + Vector2.Normalize(local) * bounds.Size * 0.25f, center.MapId);
    }
}
