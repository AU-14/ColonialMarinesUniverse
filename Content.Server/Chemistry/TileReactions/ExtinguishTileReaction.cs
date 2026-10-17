using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Fire;
using Content.Shared.CMU14.Fire;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Server.Chemistry.TileReactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class ExtinguishTileReaction : ITileReaction
    {
        [DataField("coolingTemperature")] private float _coolingTemperature = 2f;

        public FixedPoint2 TileReact(TileRef tile,
            ReagentPrototype reagent,
            FixedPoint2 reactVolume,
            IEntityManager entityManager,
            List<ReagentData>? data)
        {
            if (reactVolume <= FixedPoint2.Zero || tile.Tile.IsEmpty)
                return FixedPoint2.Zero;

            // CMU14: extinguish loose burning items.
            // Loose items do not collide with extinguisher vapour. Apply its tile
            // reaction to their fire state too, even without an atmospheric hotspot.
            var fires = entityManager.System<AU14FireSpreadSystem>();
            foreach (var uid in entityManager.System<EntityLookupSystem>().GetEntitiesInTile(tile, LookupFlags.All))
            {
                if (entityManager.TryGetComponent<FlamabilityComponent>(uid, out var burning) && burning.OnFire)
                    fires.Extinguish(uid, burning);
            }

            var atmosphereSystem = entityManager.System<AtmosphereSystem>();

            var environment = atmosphereSystem.GetTileMixture(tile.GridUid, null, tile.GridIndices, true);

            if (environment == null || !atmosphereSystem.IsHotspotActive(tile.GridUid, tile.GridIndices))
                return FixedPoint2.Zero;

            environment.Temperature =
                MathF.Max(MathF.Min(environment.Temperature - (_coolingTemperature * 1000f),
                        environment.Temperature / _coolingTemperature), Atmospherics.TCMB);

            atmosphereSystem.ReactTile(tile.GridUid, tile.GridIndices);
            atmosphereSystem.HotspotExtinguish(tile.GridUid, tile.GridIndices);

            return FixedPoint2.Zero;
        }
    }
}
