using Content.Server.Stack;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private StackSystem _stack = default!;

    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates)
    {
        EntityUid? ammoEnt = null;

        // TODO: Combine with TakeAmmo
        if (ent.Comp.Entities.Count > 0)
        {
            var existing = ent.Comp.Entities[^1];
            ent.Comp.Entities.RemoveAt(ent.Comp.Entities.Count - 1);
            DirtyField(ent.AsNullable(), nameof(BallisticAmmoProviderComponent.Entities));

            Containers.Remove(existing, ent.Comp.Container);
            EnsureShootable(existing);
        }
        else if (ent.Comp.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.UnspawnedCount));
            ent = Spawn(component.Proto, coordinates);
            _stack.SetCount(ent.Value, 1);
            EnsureShootable(ent.Value);
        }

        if (ammoEnt != null)
            EjectCartridge(ammoEnt.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(ent, ref cycledEvent);
    }
}
