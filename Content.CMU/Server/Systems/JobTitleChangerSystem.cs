using Content.Shared.Inventory.Events;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Inventory;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.AU14.Util;
using Robust.Shared.Containers;
using Content.Server._AU14.Marines.Roles.Ranks;
using Content.Shared._AU14.Marines.Roles.Ranks;

namespace Content.Server.AU14.Systems;

public sealed partial class JobTitleChangerSystem : EntitySystem
{
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private RankChangerSystem _rankChanger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JobTitleChangerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<JobTitleChangerComponent, GotUnequippedEvent>(OnUnequipped);
        // Listen for accessories being inserted/removed from the uniform accessory holder
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnAccessoryInserted);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnAccessoryRemoved);
        // If the accessory (or clothing with this component) is deleted or the component shuts down, revert titles
        SubscribeLocalEvent<JobTitleChangerComponent, ComponentShutdown>(OnJobTitleChangerShutdown);
    }

    private void OnJobTitleChangerShutdown(EntityUid uid, JobTitleChangerComponent comp, ComponentShutdown args)
    {
        if (_containers.TryGetContainingContainer(uid, out var container))
        {
            var owner = container.Owner;
            if (owner != EntityUid.Invalid && Exists(owner) && TryComp(owner, out IdCardComponent? idCard))
            {
                if (!string.IsNullOrWhiteSpace(comp.JobTitle) && idCard._jobTitle == comp.JobTitle)
                {
                    if (_minds.TryGetMind(owner, out var mindId, out _) && _jobs.MindTryGetJobName(mindId, out var jobName))
                    {
                        idCard._jobTitle = jobName;
                    }
                    else
                    {
                        idCard._jobTitle = null;
                    }
                    Dirty(owner, idCard);
                }
            }
        }
    }

    private void OnEquipped(EntityUid uid, JobTitleChangerComponent comp, GotEquippedEvent args)
    {
        // Only apply if equipped to a humanoid
        if (!TryComp(args.Equipee, out InventoryComponent? _))
            return;

        // Set the temporary job title
        if (!string.IsNullOrWhiteSpace(comp.JobTitle))
        {
            if (TryComp(args.Equipee, out IdCardComponent? idCard))
            {
                idCard._jobTitle = comp.JobTitle;
                Dirty(args.Equipee, idCard);
            }
        }
    }

    private void OnUnequipped(EntityUid uid, JobTitleChangerComponent comp, GotUnequippedEvent args)
    {
        // Only apply if unequipped from a humanoid
        if (!TryComp(args.Equipee, out InventoryComponent? _))
            return;

        // Revert to original job title
        if (TryComp(args.Equipee, out IdCardComponent? idCard))
        {
            // Try to get the mind's job name
            if (_minds.TryGetMind(args.Equipee, out var mindId, out _) &&
                _jobs.MindTryGetJobName(mindId, out var jobName))
            {
                idCard._jobTitle = jobName;
            }
            else
            {
                idCard._jobTitle = null;
            }
            Dirty(args.Equipee, idCard);
        }
    }

    private void OnAccessoryInserted(EntityUid uid, UniformAccessoryHolderComponent comp, EntInsertedIntoContainerMessage args)
{
    if (args.Container.ID != comp.ContainerId)
        return;

    if (TryComp<JobTitleChangerComponent>(args.Entity, out var changer))
    {
        if (!string.IsNullOrWhiteSpace(changer.JobTitle))
        {
            if (TryComp(uid, out IdCardComponent? idCard))
            {
                idCard._jobTitle = changer.JobTitle;
                Dirty(uid, idCard);
            }
        }
    }

    // Handle rank changing on accessory insert
    if (TryComp<RankChangerComponent>(args.Entity, out var rankChanger))
    {
        // uid is the clothing item — find who is actually wearing it
        if (_containers.TryGetContainingContainer(uid, out var wearerContainer))
            _rankChanger.ApplyRank(wearerContainer.Owner, rankChanger);
    }
}

    private void OnAccessoryRemoved(EntityUid uid, UniformAccessoryHolderComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        if (TryComp(uid, out IdCardComponent? idCard))
        {
            if (TryComp(args.Entity, out JobTitleChangerComponent? changer) &&
                idCard._jobTitle == changer.JobTitle)
            {
                if (_minds.TryGetMind(uid, out var mindId, out _) &&
                    _jobs.MindTryGetJobName(mindId, out var jobName))
                    idCard._jobTitle = jobName;
                else
                    idCard._jobTitle = null;
                Dirty(uid, idCard);
            }
        }

        // Handle rank revert on accessory remove
        if (TryComp<RankChangerComponent>(args.Entity, out var rankChanger))
        {
            if (_containers.TryGetContainingContainer(uid, out var wearerContainer))
                _rankChanger.RevertRank(wearerContainer.Owner, rankChanger);
        }
    }
}
