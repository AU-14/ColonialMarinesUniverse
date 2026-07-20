using Content.Client.Stack;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private StackSystem _stack = default!;

    protected override void InitializeBallistic()
    {
        base.InitializeBallistic();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, UpdateAmmoCounterEvent>(OnBallisticAmmoCount);
    }

    private void OnBallisticAmmoCount(Entity<BallisticAmmoProviderComponent> ent, ref UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
        {
            control.Update(GetBallisticShots(component) + args.ArtificialIncrease, component.Capacity);
        }
    }

    protected override void Cycle(Entity<BallisticAmmoProviderComponent> ent, MapCoordinates coordinates)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        EntityUid? ammoEnt = null;

        // TODO: Combine with TakeAmmo
        if (ent.Comp.Entities.Count > 0)
        {
            var existing = ent.Comp.Entities[^1];
            ent.Comp.Entities.RemoveAt(ent.Comp.Entities.Count - 1);

            Containers.Remove(existing, ent.Comp.Container);
            EnsureShootable(existing);
        }
        else if (ent.Comp.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            ent = Spawn(component.Proto, coordinates);
            _stack.SetCount(ent.Value, 1);
            EnsureShootable(ent.Value);
        }

        if (ammoEnt != null && IsClientSide(ammoEnt.Value))
            Del(ammoEnt.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(ent, ref cycledEvent);
    }
}
