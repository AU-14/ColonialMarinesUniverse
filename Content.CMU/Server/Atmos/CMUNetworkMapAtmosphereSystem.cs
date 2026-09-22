using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Fills network member maps that ship no MapAtmosphere with the network's
/// base declaration: the lowest deck that authors one. Authored decks are
/// never touched, so deliberate per-deck variations stand, while the easy
/// scheme of one component on the base deck serves the whole network. Filled
/// decks read planet air, outdoor relaxation gates on, and the day-night
/// driver picks them up. Idempotent; runs on every network topology change.
/// </summary>
public sealed class CMUNetworkMapAtmosphereSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly CMUOutdoorAtmosphereSystem _outdoor = default!;

    private EntityQuery<MapAtmosphereComponent> _mapAtmosQuery = default!;

    public override void Initialize()
    {
        _mapAtmosQuery = GetEntityQuery<MapAtmosphereComponent>();

        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnZNetworkUpdated);
    }

    private void OnZNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
        => EnsureNetworkInherited(args.Network);

    public void EnsureNetworkInherited(Entity<CMUZLevelsNetworkComponent> network)
    {
        if (TerminatingOrDeleted(network))
            return;

        // The base is the lowest declarer; a declaration at any other depth
        // stays authored and only serves its own deck.
        EntityUid? baseMap = null;
        var baseDepth = int.MaxValue;
        MapAtmosphereComponent? baseAtmos = null;

        foreach (var (depth, member) in network.Comp.ZLevels)
        {
            if (member is not { } map
                || depth >= baseDepth
                || !_mapAtmosQuery.TryComp(map, out var atmos))
                continue;

            baseDepth = depth;
            baseMap = map;
            baseAtmos = atmos;
        }

        if (baseMap is not { } declared)
            return;

        foreach (var member in network.Comp.ZLevels.Values)
        {
            if (member is not { } map
                || map == declared
                || _mapAtmosQuery.HasComp(map))
                continue;

            // Immutable base mixture is shared by reference, the same way the
            // engine shares SpaceGas across maps; SetMapAtmosphere sanitizes.
            _atmosphere.SetMapAtmosphere(map, baseAtmos!.Space, baseAtmos.Mixture);
            _outdoor.RefreshForMap(map);
        }
    }
}
