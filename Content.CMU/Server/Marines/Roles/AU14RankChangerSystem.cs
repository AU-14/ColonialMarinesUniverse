using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared._AU14.Marines.Roles.Ranks;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Hands;
using Content.Server._RMC14.Marines.Roles.Ranks;

namespace Content.Server._AU14.Marines.Roles.Ranks;

public sealed partial class RankChangerSystem : EntitySystem
{
    [Dependency] private RankSystem _rank = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RankChangerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<RankChangerComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<RankChangerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RankChangerComponent, GotEquippedHandEvent>(OnEquippedHand);
    SubscribeLocalEvent<RankChangerComponent, GotUnequippedHandEvent>(OnUnequippedHand);
    }

    private void OnEquipped(Entity<RankChangerComponent> ent, ref GotEquippedEvent args)
    {
        ApplyRank(args.Equipee, ent.Comp);
    }

    private void OnUnequipped(Entity<RankChangerComponent> ent, ref GotUnequippedEvent args)
    {
        RevertRank(args.Equipee, ent.Comp);
    }

    private void OnShutdown(Entity<RankChangerComponent> ent, ref ComponentShutdown args)
    {
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
            RevertRank(container.Owner, ent.Comp);
    }

    private void OnAccessoryInserted(EntityUid uid, UniformAccessoryHolderComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        if (TryComp<RankChangerComponent>(args.Entity, out var changer))
            ApplyRank(uid, changer);
    }

    private void OnAccessoryRemoved(EntityUid uid, UniformAccessoryHolderComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        if (TryComp<RankChangerComponent>(args.Entity, out var changer))
            RevertRank(uid, changer);
    }

    public void ApplyRank(EntityUid wearer, RankChangerComponent comp)
    {
        if (!_prototypes.TryIndex(comp.Rank, out var rankProto))
            return;

        _rank.SetRank(wearer, rankProto);
    }

    public void RevertRank(EntityUid wearer, RankChangerComponent comp)
    {
        if (!TryComp<RankComponent>(wearer, out var rankComp) || rankComp.Rank != comp.Rank)
            return;

        // Check if another RankChanger is still active
        var enumerator = AllComps<RankChangerComponent>(wearer);
        foreach (var other in enumerator)
        {
            if (other == comp)
                continue;

            if (_prototypes.TryIndex(other.Rank, out var otherProto))
            {
                _rank.SetRank(wearer, otherProto);
                return;
            }
        }

        // Restore job rank instead of clearing
        _rank.ReapplyJobRank(wearer);
    }

    private void OnEquippedHand(Entity<RankChangerComponent> ent, ref GotEquippedHandEvent args)
    {
        ApplyRank(args.User, ent.Comp);
    }

    private void OnUnequippedHand(Entity<RankChangerComponent> ent, ref GotUnequippedHandEvent args)
    {
        RevertRank(args.User, ent.Comp);
    }
}