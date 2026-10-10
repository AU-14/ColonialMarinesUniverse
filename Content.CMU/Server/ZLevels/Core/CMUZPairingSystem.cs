using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed class CMUZPairingSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;

    private readonly HashSet<Entity<CMUZPairedComponent>> _candidates = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUZPairedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUZPairedComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CMUZPairedComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnStartup(Entity<CMUZPairedComponent> ent, ref ComponentStartup args)
    {
        if (Transform(ent).Anchored)
            TryPair(ent);
    }

    private void OnAnchorChanged(Entity<CMUZPairedComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            TryPair(ent);
        else
            Unpair(ent);
    }

    private void OnTerminating(Entity<CMUZPairedComponent> ent, ref EntityTerminatingEvent args)
    {
        Unpair(ent);
    }

    public void TryPair(Entity<CMUZPairedComponent> ent)
    {
        if (ent.Comp.Twin != null || ent.Comp.Offset == 0)
            return;

        var xform = Transform(ent);
        if (xform.MapUid is not { } map)
            return;

        if (!_zLevels.TryProjectToZMap((map, null), ent.Comp.Offset, _transform.GetWorldPosition(xform), out var coords, out _))
            return;

        _candidates.Clear();
        _lookup.GetEntitiesInRange(coords, 0.49f, _candidates);

        foreach (var other in _candidates)
        {
            if (other.Owner == ent.Owner
                || other.Comp.Twin != null
                || other.Comp.PairKind != ent.Comp.PairKind
                || other.Comp.Offset + ent.Comp.Offset != 0
                || !Transform(other).Anchored)
            {
                continue;
            }

            ent.Comp.Twin = other.Owner;
            other.Comp.Twin = ent.Owner;
            Dirty(ent);
            Dirty(other);
            var ev = new CMUZPairedEvent(other.Owner);
            RaiseLocalEvent(ent, ref ev);
            ev = new CMUZPairedEvent(ent.Owner);
            RaiseLocalEvent(other, ref ev);
            return;
        }
    }

    public void Unpair(Entity<CMUZPairedComponent> ent)
    {
        if (ent.Comp.Twin is not { } twin)
            return;

        ent.Comp.Twin = null;
        Dirty(ent);
        var ev = new CMUZUnpairedEvent(twin);
        RaiseLocalEvent(ent, ref ev);

        if (!TryComp<CMUZPairedComponent>(twin, out var twinComp))
            return;

        twinComp.Twin = null;
        Dirty(twin, twinComp);
        ev = new CMUZUnpairedEvent(ent.Owner);
        RaiseLocalEvent(twin, ref ev);
    }
}
