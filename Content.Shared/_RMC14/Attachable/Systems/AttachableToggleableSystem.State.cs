using Content.Shared._RMC14.Attachable.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Attachable.Systems;

public sealed partial class AttachableToggleableSystem
{
    [SubscribeLocalEvent]
    private void OnAttachableDirectionLockedComponentGetState(Entity<AttachableDirectionLockedComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        var netAttachableList = new List<NetEntity>();
        foreach (var uid in ent.Comp.AttachableList)
        {
            if (TryGetNetEntity(uid, out var net) && net != NetEntity.Invalid)
                netAttachableList.Add(net.Value);
        }

        args.State = new AttachableDirectionLockedComponentState
        {
            AttachableList = netAttachableList,
            LockedDirection = ent.Comp.LockedDirection,
        };
    }

    [SubscribeLocalEvent]
    private void OnAttachableDirectionLockedComponentHandleState(Entity<AttachableDirectionLockedComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not AttachableDirectionLockedComponentState state)
            return;

        EnsureEntityList<AttachableDirectionLockedComponent>(state.AttachableList, ent, ent.Comp.AttachableList);
        ent.Comp.LockedDirection = state.LockedDirection;
    }
}

[Serializable, NetSerializable]
public sealed class AttachableDirectionLockedComponentState : ComponentState
{
    public List<NetEntity> AttachableList { get; init; } = new();
    public Direction? LockedDirection { get; init; }
}
