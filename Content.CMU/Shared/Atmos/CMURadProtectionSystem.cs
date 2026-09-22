using Content.Shared.Armor;
using Content.Shared.Inventory;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Collects wearable rad shielding onto <see cref="GetRadProtectionEvent"/>.
/// Mirrors the fire stack: each shielded item reduces the dose, and a suit
/// whose head is not itself shielded gives part of its reduction back.
/// </summary>
public sealed class CMURadProtectionSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMURadProtectionComponent, InventoryRelayedEvent<GetRadProtectionEvent>>(OnProtection);
        SubscribeLocalEvent<CMURadCoverageComponent, InventoryRelayedEvent<GetRadProtectionEvent>>(OnCoverage);
        SubscribeLocalEvent<CMURadProtectionComponent, ArmorExamineEvent>(OnArmorExamine);
    }

    private void OnProtection(Entity<CMURadProtectionComponent> ent, ref InventoryRelayedEvent<GetRadProtectionEvent> args)
    {
        args.Args.Reduce(ent.Comp.Reduction);
    }

    private void OnArmorExamine(Entity<CMURadProtectionComponent> ent, ref ArmorExamineEvent args)
    {
        var value = MathF.Round(ent.Comp.Reduction * 100, 1);

        if (value == 0)
            return;

        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString(ent.Comp.ExamineMessage, ("value", value)));
    }

    private void OnCoverage(Entity<CMURadCoverageComponent> ent, ref InventoryRelayedEvent<GetRadProtectionEvent> args)
    {
        if (IsHeadShielded(args.Owner, ent.Comp.MinHeadReduction))
            return;

        args.Args.Multiplier += ent.Comp.UncoveredHeadPenalty;
    }

    // A shielded head, not just any helmet: token protection eases the dose
    // but does not restore the suit's own rating.
    private bool IsHeadShielded(EntityUid wearer, float minReduction)
    {
        if (!_inventory.TryGetSlotEntity(wearer, "head", out var head))
            return false;

        return TryComp<CMURadProtectionComponent>(head, out var rad)
            && rad.Reduction >= minReduction;
    }
}
