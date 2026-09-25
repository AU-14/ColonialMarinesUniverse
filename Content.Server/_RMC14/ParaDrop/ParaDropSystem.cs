using Content.Server._RMC14.Dropship;
using Content.Shared.Doors.Components;
using Content.Shared.ParaDrop;
using Content.Shared.CMU14.Dropship.MultiDeck;

namespace Content.Server._RMC14.ParaDrop;

public sealed partial class ParaDropSystem: SharedParaDropSystem
{
    [Dependency] private DropshipSystem _dropship = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActiveParaDropComponent, ComponentShutdown>(OnActiveParaDropShutdown);
    }

    private void OnActiveParaDropShutdown(Entity<ActiveParaDropComponent> ent, ref ComponentShutdown args)
    {
        var changed = new DropshipParadropChangedEvent(false);
        RaiseLocalEvent(ent, ref changed);
        var enumerator = Transform(ent).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!TryComp(child, out DoorBoltComponent? bolt) ||
                !TryComp(child, out DoorComponent? door) ||
                door.Location != DoorLocation.Aft)
                continue;

            _dropship.UnlockDoor(child);
            _dropship.LockDoor(child);
        }
    }
}
