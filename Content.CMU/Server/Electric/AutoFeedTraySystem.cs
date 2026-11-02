// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Examine;

namespace Content.Server.CMU14.Electric;

// The automatic hydroponics tray: once a second, with power, it takes water and fertilizer out of its tank into the tray whenever
// the tray has less than the threshold of either.
public sealed class AutoFeedTraySystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly PlantTraySystem _tray = default!;
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly PlantHarvestSystem _harvest = default!;
    [Dependency] private readonly PlantHolderSystem _holder = default!;
    [Dependency] private readonly PlantSystem _plants = default!;

    private const float Interval = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoFeedTrayComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AutoFeedTrayComponent, PlantTrayComponent>();
        while (query.MoveNext(out var uid, out var feed, out var tray))
        {
            feed.NextFeed -= frameTime;
            if (feed.NextFeed > 0f)
                continue;

            feed.NextFeed = Interval;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            // Water: a unit of water is a point of water level.
            var water = tray.MaxWaterLevel * feed.Threshold - tray.WaterLevel;
            if (water > 0f)
            {
                var taken = _fluids.Remove(uid, FluidKinds.Water, Math.Min(water, feed.WaterPerSecond));
                if (taken > 0f)
                    _tray.AdjustWater((uid, tray), taken);
            }

            // Nutrients: the fertilizer is worth several points of nutrition a unit.
            var nutrition = tray.MaxNutritionLevel * feed.Threshold - tray.NutritionLevel;
            if (nutrition > 0f)
            {
                var wanted = Math.Min(nutrition / feed.NutritionPerUnit, feed.FertilizerPerSecond);
                var taken = _fluids.Remove(uid, feed.FertilizerKind, wanted);
                if (taken > 0f)
                    _tray.AdjustNutrient((uid, tray), taken * feed.NutritionPerUnit);
            }

            Farm((uid, feed, tray));
        }
    }

    // Harvests the ripe plant and plants the same again where there is none.
    private void Farm(Entity<AutoFeedTrayComponent, PlantTrayComponent> ent)
    {
        var (uid, feed, tray) = ent;

        if (_tray.TryGetPlant((uid, tray), out var plant))
        {
            Remember(ent, plant.Value);

            if (!TryComp<PlantHolderComponent>(plant, out var holder))
                return;

            if (_holder.IsDead(plant.Value))
            {
                // A dead plant is cleared away, and the next tick plants a new one.
                if (feed.AutoReplant)
                    _plants.RemovePlant(plant.Value);

                return;
            }

            if (feed.AutoHarvest && holder.ReadyForHarvest)
                _harvest.TryHandleHarvest(plant.Value, uid);

            return;
        }

        if (feed.AutoReplant && feed.PlantProto is { } proto)
        {
            var next = Spawn(proto, Transform(uid).Coordinates);
            _botany.ApplyPlantSnapshotData(feed.Snapshot, next);
            _tray.PlantingPlantInTray((uid, tray), next);
            feed.Remembered = next;
        }
    }

    // Keeps a copy of the data of the plant in the tray, so that the same one can be planted after the harvest. A plant without seeds
    // is not kept, and is not planted again.
    private void Remember(Entity<AutoFeedTrayComponent, PlantTrayComponent> ent, EntityUid plant)
    {
        var feed = ent.Comp1;
        if (feed.Remembered == plant)
            return;

        feed.Remembered = plant;

        if (feed.Snapshot is { } old)
            QueueDel(old);

        feed.Snapshot = null;
        feed.PlantProto = null;

        var proto = MetaData(plant).EntityPrototype?.ID;
        if (proto == null)
            return;

        var snapshot = _botany.ClonePlantSnapshotData(plant, ent.Owner);
        if (_botany.TryGetPlantComponent<PlantTraitSeedlessComponent>(snapshot, proto, out _))
        {
            if (snapshot != null)
                QueueDel(snapshot);

            return;
        }

        feed.PlantProto = proto;
        feed.Snapshot = snapshot;
    }

    private void OnExamined(Entity<AutoFeedTrayComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(this.IsPowered(ent.Owner, EntityManager) ? "au14-autotray-examine-powered" : "au14-autotray-examine-unpowered",
            ("water", (int) _fluids.GetAmount(ent.Owner, FluidKinds.Water)),
            ("fertilizer", (int) _fluids.GetAmount(ent.Owner, ent.Comp.FertilizerKind))));
    }
}
