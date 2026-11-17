using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Items.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
/// Marks produce harvested from any seed stocked by the MegaSeed servitor as an ASRS export.
/// Reading the vending inventory keeps this in sync when its seed selection changes.
/// </summary>
public sealed class CMUMegaSeedProduceSystem : EntitySystem
{
    private const string MegaSeedInventory = "MegaSeedServitorInventory";

    private readonly HashSet<EntProtoId> _sellableSeedPackets = [];

    public override void Initialize()
    {
        base.Initialize();
        BuildSeedPacketCache();
        SubscribeLocalEvent<ProduceComponent, ProduceSpawnedEvent>(OnProduceSpawned);
    }

    private void BuildSeedPacketCache()
    {
        _sellableSeedPackets.Clear();
        var inventory = ProtoMan.Index<VendingMachineInventoryPrototype>(MegaSeedInventory);

        foreach (var prototype in inventory.StartingInventory.Keys)
            _sellableSeedPackets.Add(prototype);

        if (inventory.ContrabandInventory is { } contraband)
        {
            foreach (var prototype in contraband.Keys)
                _sellableSeedPackets.Add(prototype);
        }
    }

    private void OnProduceSpawned(Entity<ProduceComponent> produce, ref ProduceSpawnedEvent args)
    {
        if (!_sellableSeedPackets.Contains(args.SeedPacket) ||
            !HasComp<PlantHolderComponent>(args.PlantHolder) ||
            !HasComp<PlantTrayComponent>(Transform(args.PlantHolder).ParentUid))
            return;

        EnsureComp<ASRSSellableComponent>(produce);
    }
}
