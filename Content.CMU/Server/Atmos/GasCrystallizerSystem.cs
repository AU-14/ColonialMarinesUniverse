using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

[UsedImplicitly]
public sealed partial class GasCrystallizerSystem : EntitySystem
{
    [Dependency] private NodeContainerSystem _nodeContainer = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GasCrystallizerComponent, AtmosDeviceUpdateEvent>(OnUpdated);
    }

    private void OnUpdated(Entity<GasCrystallizerComponent> entity, ref AtmosDeviceUpdateEvent args)
    {
        if (!TryComp<ApcPowerReceiverComponent>(entity, out var receiver)
            || !_power.IsPowered(entity, receiver)
            || _timing.CurTime < entity.Comp.NextBatch
            || !Transform(entity).Anchored
            || !_nodeContainer.TryGetNode(entity.Owner, entity.Comp.Inlet, out PipeNode? inlet))
            return;

        var air = inlet.Air;
        foreach (var recipe in entity.Comp.Recipes)
        {
            if (air.Temperature < recipe.MinTemperature || air.Temperature > recipe.MaxTemperature)
                continue;

            var satisfied = true;
            foreach (var (gas, moles) in recipe.Gases)
            {
                if (air.GetMoles(gas) < moles)
                {
                    satisfied = false;
                    break;
                }
            }

            if (!satisfied)
                continue;

            foreach (var (gas, moles) in recipe.Gases)
                air.AdjustMoles(gas, -moles);

            var output = Spawn(recipe.Output, Transform(entity).Coordinates);
            if (TryComp<StackComponent>(output, out var stack))
                _stack.SetCount(output, recipe.OutputAmount, stack);
            _stack.TryMergeToContacts(output);

            entity.Comp.NextBatch = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.BatchDelay);
            return; // one batch per cycle
        }
    }
}
