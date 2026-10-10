using Content.Shared.Atmos;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared._RMC14.Atmos;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Fire outerwear protects a fire-rated head, not a bare one. While the head slot
/// holds no item with fire protection or ignition resistance, the suit's ignition
/// immunity is voided and part of its burn reduction is given back.
/// </summary>
public sealed class CMUFireCoverageSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        // The ignition veto must land after the suit's own immunity handler, or the
        // suit would set Ignite back to false afterwards. The protection penalty is
        // additive, so its ordering does not matter.
        SubscribeLocalEvent<CMUFireCoverageComponent, InventoryRelayedEvent<GetIgnitionImmunityEvent>>(OnIgnition,
            after: new[] { typeof(SharedRMCFlammableSystem) });
        SubscribeLocalEvent<CMUFireCoverageComponent, InventoryRelayedEvent<GetFireProtectionEvent>>(OnProtection,
            after: new[] { typeof(FireProtectionSystem) });
    }

    private void OnIgnition(Entity<CMUFireCoverageComponent> ent, ref InventoryRelayedEvent<GetIgnitionImmunityEvent> args)
    {
        if (args.Args.Ignite || IsHeadCovered(args.Owner, ent.Comp.MinHeadReduction))
            return;

        args.Args.Ignite = true;
    }

    private void OnProtection(Entity<CMUFireCoverageComponent> ent, ref InventoryRelayedEvent<GetFireProtectionEvent> args)
    {
        if (IsHeadCovered(args.Owner, ent.Comp.MinHeadReduction))
            return;

        args.Args.Multiplier += ent.Comp.UncoveredHeadPenalty;
    }

    // A rated head, not just any helmet: token protection eases the burn but does
    // not restore the suit's own rating.
    private bool IsHeadCovered(EntityUid wearer, float minReduction)
    {
        if (!_inventory.TryGetSlotEntity(wearer, "head", out var head))
            return false;

        if (HasComp<RMCImmuneToIgnitionComponent>(head))
            return true;

        return TryComp<FireProtectionComponent>(head, out var fire)
            && fire.Reduction >= minReduction;
    }
}
