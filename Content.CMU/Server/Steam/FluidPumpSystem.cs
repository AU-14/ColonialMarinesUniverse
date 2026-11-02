// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Portable;
using Content.Shared.CMU14.Steam;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Steam;

// The pump draws water into its own tank, a little every second, but only where it stands on water: a pool, a lake, any of the
// water of the floor. Off the water it pumps nothing. How fast the water goes into the pipes is up to the port of the pump,
// which is slower still.
public sealed class FluidPumpSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    // The water of the floor, and everything that is made of it: the water of the base game and of the caves (FloorWaterEntity), and the water
    // of the RMC maps and of the planets made from it (CMFloorShallowWaterEntity, the deep water and the rivers come from it).
    private static readonly EntProtoId[] WaterEntities = { "FloorWaterEntity", "CMFloorShallowWaterEntity" };

    // How often a pump looks again at what it stands on.
    private const float CheckInterval = 1f;

    private readonly HashSet<EntityUid> _found = new();
    private readonly Dictionary<string, bool> _isWater = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FluidPumpComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FluidPumpComponent, PortablePlaceableOverEvent>(OnPlaceableOver);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FluidPumpComponent>();
        while (query.MoveNext(out var uid, out var pump))
        {
            pump.CheckIn -= frameTime;
            if (pump.CheckIn <= 0f)
            {
                pump.CheckIn = CheckInterval;
                pump.OnWater = StandsOnWater(uid);
            }

            if (pump.OnWater)
                _fluids.Add(uid, FluidKinds.Water, pump.WaterPerSecond * frameTime);
        }
    }

    // There is water of the floor on the tile of the pump.
    public bool StandsOnWater(EntityUid pump)
    {
        _found.Clear();
        _lookup.GetEntitiesInRange(Transform(pump).Coordinates, 0.45f, _found, LookupFlags.All);

        foreach (var entity in _found)
        {
            if (entity != pump && MetaData(entity).EntityPrototype is { } proto && IsWater(proto))
                return true;
        }

        return false;
    }

    private bool IsWater(EntityPrototype proto)
    {
        if (_isWater.TryGetValue(proto.ID, out var known))
            return known;

        var water = false;
        foreach (var parent in _proto.EnumerateParents<EntityPrototype>(proto.ID, includeSelf: true))
        {
            if (Array.IndexOf(WaterEntities, (EntProtoId) parent.ID) >= 0)
            {
                water = true;
                break;
            }
        }

        _isWater[proto.ID] = water;
        return water;
    }

    // The pump is made to stand on water, so the water on the tile is not in its way.
    private void OnPlaceableOver(Entity<FluidPumpComponent> pump, ref PortablePlaceableOverEvent args)
    {
        if (MetaData(args.Other).EntityPrototype is { } proto && IsWater(proto))
            args.Allowed = true;
    }

    private void OnExamined(Entity<FluidPumpComponent> pump, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(StandsOnWater(pump) ? "au14-pump-examine-water" : "au14-pump-examine-dry"));
    }
}
