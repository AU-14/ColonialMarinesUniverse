// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Power.Components;
using Content.Shared.CMU14.Electric;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Electric;

// The generators of the earth: the geothermal generator counts the lava that lies beside it and gives its share of the power for every piece.
public sealed class EnergySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private readonly HashSet<Entity<MetaDataComponent>> _near = new();
    private TimeSpan _nextCheck;

    // How often the sources are counted: the pools of lava do not move.
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1.5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SourceGeneratorComponent, ExaminedEvent>(OnSourceExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckEvery;

        var sources = EntityQueryEnumerator<SourceGeneratorComponent, PowerSupplierComponent, TransformComponent>();
        while (sources.MoveNext(out var uid, out var gen, out var supplier, out var xform))
        {
            gen.Found = CountSources(uid, gen, xform);
            Apply(uid, supplier, gen.Found * gen.SupplyPerSource);
        }
    }

    private void Apply(EntityUid uid, PowerSupplierComponent supplier, float supply)
    {
        var enabled = supply > 0f;
        if (supplier.Enabled != enabled)
            supplier.Enabled = enabled;

        if (enabled && Math.Abs(supplier.MaxSupply - supply) > 1f)
            supplier.MaxSupply = supply;

        _appearance.SetData(uid, RecipeMachineVisuals.Working, enabled);
    }

    private int CountSources(EntityUid uid, SourceGeneratorComponent gen, TransformComponent xform)
    {
        _near.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, gen.Range, _near);

        var found = 0;
        foreach (var entity in _near)
        {
            if (entity.Owner != uid && entity.Comp.EntityPrototype?.ID == gen.Source)
                found++;
        }

        return Math.Min(found, gen.MaxSources);
    }

    private void OnSourceExamined(Entity<SourceGeneratorComponent> gen, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(gen.Comp.Found > 0 ? "au14-source-examine" : "au14-source-examine-none", ("count", gen.Comp.Found), ("power", (int) (gen.Comp.Found * gen.Comp.SupplyPerSource))));
    }
}
