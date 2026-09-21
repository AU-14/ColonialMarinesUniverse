using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Armor.Magnetic;

public sealed partial class RMCMagneticSystem
{
    [SubscribeLocalEvent]
    private void OnRMCReturnToInventoryComponentGetState(Entity<RMCReturnToInventoryComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.User, out var netUser);
        TryGetNetEntity(ent.Comp.Magnetizer, out var netMagnetizer);
        TryGetNetEntity(ent.Comp.ReceivingItem, out var netReceivingItem);
        args.State = new RMCReturnToInventoryComponentState
        {
            User = netUser ?? NetEntity.Invalid,
            Magnetizer = netMagnetizer ?? NetEntity.Invalid,
            Returned = ent.Comp.Returned,
            ReceivingItem = netReceivingItem,
            ReceivingContainer = ent.Comp.ReceivingContainer,
        };
    }

    [SubscribeLocalEvent]
    private void OnRMCReturnToInventoryComponentHandleState(Entity<RMCReturnToInventoryComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not RMCReturnToInventoryComponentState state)
            return;

        ent.Comp.User = EnsureEntity<RMCReturnToInventoryComponent>(state.User, ent);
        ent.Comp.Magnetizer = EnsureEntity<RMCReturnToInventoryComponent>(state.Magnetizer, ent);
        ent.Comp.Returned = state.Returned;
        ent.Comp.ReceivingItem = EnsureEntity<RMCReturnToInventoryComponent>(state.ReceivingItem, ent);
        ent.Comp.ReceivingContainer = state.ReceivingContainer;
    }
}

[Serializable, NetSerializable]
public sealed class RMCReturnToInventoryComponentState : ComponentState
{
    public NetEntity User { get; init; }
    public NetEntity Magnetizer { get; init; }
    public bool Returned { get; init; }
    public NetEntity? ReceivingItem { get; init; }
    public string ReceivingContainer { get; init; } = string.Empty;
}
