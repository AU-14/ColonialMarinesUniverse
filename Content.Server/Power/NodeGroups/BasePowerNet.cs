using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Pow3r;
using Content.Shared.NodeContainer;
using Robust.Shared.Utility;

namespace Content.Server.Power.NodeGroups;

public abstract class BasePowerNet<TNetType> : BaseNetConnectorNodeGroup<TNetType>, IBasePowerNet
    where TNetType : IBasePowerNet
{
    [ViewVariables] public readonly List<Entity<PowerConsumerComponent>> Consumers = new();
    [ViewVariables] public readonly List<Entity<PowerSupplierComponent>> Suppliers = new();
    public PowerNetSystem PowerNetSystem = default!;

    [ViewVariables]
    public PowerState.Network NetworkNode { get; } = new();

    public override void Initialize(Node sourceNode, IEntityManager entMan)
    {
        base.Initialize(sourceNode, entMan);
        PowerNetSystem = entMan.EntitySysManager.GetEntitySystem<PowerNetSystem>();
    }

    public bool IsConnectedNetwork => NodeCount > 1;

    public void AddConsumer(EntityUid uid, PowerConsumerComponent consumer)
    {
        DebugTools.Assert(consumer.NetworkLoad.LinkedNetwork == default);
        consumer.NetworkLoad.LinkedNetwork = default;
        Consumers.Add((uid, consumer));
        QueueNetworkReconnect();
    }

    public void RemoveConsumer(EntityUid uid, PowerConsumerComponent consumer)
    {
        // Linked network can be default if it was re-connected twice in one tick.
        DebugTools.Assert(consumer.NetworkLoad.LinkedNetwork == default || consumer.NetworkLoad.LinkedNetwork == NetworkNode.Id);
        consumer.NetworkLoad.LinkedNetwork = default;
        Consumers.Remove((uid, consumer));
        QueueNetworkReconnect();
    }

    public void AddSupplier(EntityUid uid, PowerSupplierComponent supplier)
    {
        DebugTools.Assert(supplier.NetworkSupply.LinkedNetwork == default);
        supplier.NetworkSupply.LinkedNetwork = default;
        Suppliers.Add((uid, supplier));
        QueueNetworkReconnect();
    }

    public void RemoveSupplier(EntityUid uid, PowerSupplierComponent supplier)
    {
        // Linked network can be default if it was re-connected twice in one tick.
        DebugTools.Assert(supplier.NetworkSupply.LinkedNetwork == default || supplier.NetworkSupply.LinkedNetwork == NetworkNode.Id);
        supplier.NetworkSupply.LinkedNetwork = default;
        Suppliers.Remove((uid, supplier));
        QueueNetworkReconnect();
    }

    public abstract void QueueNetworkReconnect();
}
