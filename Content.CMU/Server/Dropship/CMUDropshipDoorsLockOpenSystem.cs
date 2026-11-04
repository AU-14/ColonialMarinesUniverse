using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Dropship;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship;

/// <summary>
///     Opens every door on the dropship and bolts it open. The ordinary lock buttons close them again.
/// </summary>
public sealed class CMUDropshipDoorsLockOpenSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<Entity<DoorComponent>> _doors = new();
    private readonly List<DoorBoltComponent> _bolts = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDropshipDoorsLockOpenComponent, CMUDropshipLockDoorsOpenMsg>(OnLockDoorsOpen);
    }

    private void OnLockDoorsOpen(Entity<CMUDropshipDoorsLockOpenComponent> ent, ref CMUDropshipLockDoorsOpenMsg args)
    {
        if (_transform.GetGrid(ent.Owner) is not { } grid ||
            !TryComp(grid, out DropshipComponent? dropship) ||
            dropship.Crashed)
        {
            return;
        }

        // Like the side door locks, not while the dropship is in the air.
        if (TryComp(grid, out FTLComponent? ftl) && ftl.State is FTLState.Travelling or FTLState.Arriving)
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.LastUsed + ent.Comp.Cooldown)
            return;

        ent.Comp.LastUsed = now;

        TryComp(grid, out MohawkMechanismsComponent? mechanisms);
        _doors.Clear();
        _bolts.Clear();
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<DockingComponent>(child) &&
                TryComp(child, out DoorBoltComponent? bolt) &&
                TryComp(child, out DoorComponent? door))
            {
                _doors.Add((child, door));
                _bolts.Add(bolt);
            }
        }

        for (var i = 0; i < _doors.Count; i++)
        {
            var door = _doors[i];
            var bolt = _bolts[i];
            // Sabotaged side controls can't work their doors.
            if (mechanisms != null &&
                (door.Comp.Location == DoorLocation.Port && mechanisms.BrokenControls.Contains(MohawkControlGroup.Port) ||
                 door.Comp.Location == DoorLocation.Starboard && mechanisms.BrokenControls.Contains(MohawkControlGroup.Starboard)))
            {
                continue;
            }

            _door.SetBoltsDown((door.Owner, bolt), false);
            if (door.Comp.State is DoorState.Closed or DoorState.Closing)
                _door.StartOpening(door, door.Comp);

            _door.SetBoltsDown((door.Owner, bolt), true);
        }
    }
}
