using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Power;

public abstract partial class SharedRMCPowerSystem
{
    private void InitializeCMUAreaPowerState()
    {
        SubscribeLocalEvent<RMCAreaPowerComponent, ComponentGetState>(OnCMUAreaPowerGetState);
        SubscribeLocalEvent<RMCAreaPowerComponent, ComponentHandleState>(OnCMUAreaPowerHandleState);
    }

    private void OnCMUAreaPowerGetState(Entity<RMCAreaPowerComponent> ent, ref ComponentGetState args)
    {
        // PVS workers may capture the same area for several clients. Filter new snapshots only;
        // lifecycle cleanup owns mutation of the gameplay collections on the simulation thread.
        args.State = new CMUAreaPowerComponentState
        {
            Apcs = GetCMULivePowerMembers(ent.Comp.Apcs),
            EquipmentReceivers = GetCMULivePowerMembers(ent.Comp.EquipmentReceivers),
            LightingReceivers = GetCMULivePowerMembers(ent.Comp.LightingReceivers),
            EnvironmentReceivers = GetCMULivePowerMembers(ent.Comp.EnvironmentReceivers),
            Load = (int[]) ent.Comp.Load.Clone(),
        };
    }

    private HashSet<NetEntity> GetCMULivePowerMembers(HashSet<EntityUid> members)
    {
        var result = new HashSet<NetEntity>();
        foreach (var member in members)
        {
            if (TryGetNetEntity(member, out var net) && net != NetEntity.Invalid)
                result.Add(net.Value);
        }
        return result;
    }

    private void OnCMUAreaPowerHandleState(Entity<RMCAreaPowerComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not CMUAreaPowerComponentState state)
            return;

        EnsureEntitySet<RMCAreaPowerComponent>(state.Apcs, ent, ent.Comp.Apcs);
        EnsureEntitySet<RMCAreaPowerComponent>(state.EquipmentReceivers, ent, ent.Comp.EquipmentReceivers);
        EnsureEntitySet<RMCAreaPowerComponent>(state.LightingReceivers, ent, ent.Comp.LightingReceivers);
        EnsureEntitySet<RMCAreaPowerComponent>(state.EnvironmentReceivers, ent, ent.Comp.EnvironmentReceivers);
        ent.Comp.Load = (int[]) state.Load.Clone();
    }
}

[Serializable, NetSerializable]
public sealed class CMUAreaPowerComponentState : ComponentState
{
    public HashSet<NetEntity> Apcs { get; init; } = new();
    public HashSet<NetEntity> EquipmentReceivers { get; init; } = new();
    public HashSet<NetEntity> LightingReceivers { get; init; } = new();
    public HashSet<NetEntity> EnvironmentReceivers { get; init; } = new();
    public int[] Load { get; init; } = [];
}
