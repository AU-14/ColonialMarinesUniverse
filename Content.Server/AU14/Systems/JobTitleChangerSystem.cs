using Content.Shared.Inventory.Events;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Inventory;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Au14.Util;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.au14.Systems;

public sealed class JobTitleChangerSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly JobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JobTitleChangerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<JobTitleChangerComponent, GotUnequippedEvent>(OnUnequipped);
        // Listen for accessories being inserted/removed from the uniform accessory holder
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnAccessoryInserted);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnAccessoryRemoved);
    }

    private void OnEquipped(EntityUid uid, JobTitleChangerComponent comp, GotEquippedEvent args)
    {
        // Only apply if equipped to a humanoid
        if (!EntityManager.TryGetComponent(args.Equipee, out InventoryComponent? inventory))
            return;

        // Set the temporary job title
        if (!string.IsNullOrWhiteSpace(comp.JobTitle))
        {
            if (EntityManager.TryGetComponent(args.Equipee, out IdCardComponent? idCard))
            {
                idCard._jobTitle = comp.JobTitle;
                Dirty(args.Equipee, idCard);
            }
        }
    }

    private void OnUnequipped(EntityUid uid, JobTitleChangerComponent comp, GotUnequippedEvent args)
    {
        // Only apply if unequipped from a humanoid
        if (!EntityManager.TryGetComponent(args.Equipee, out InventoryComponent? inventory))
            return;

        // Revert to original job title
        if (EntityManager.TryGetComponent(args.Equipee, out IdCardComponent? idCard))
        {
            // Try to get the mind's job name
            if (_minds.TryGetMind(args.Equipee, out var mindId, out var mind) &&
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
        // Only care about our accessory container
        if (args.Container.ID != comp.ContainerId)
            return;

        // If the inserted entity has a JobTitleChangerComponent, set the job title
        if (EntityManager.TryGetComponent<JobTitleChangerComponent>(args.Entity, out var changer))
        {
            if (!string.IsNullOrWhiteSpace(changer.JobTitle))
            {
                if (EntityManager.TryGetComponent(uid, out IdCardComponent? idCard))
                {
                    idCard._jobTitle = changer.JobTitle;
                    Dirty(uid, idCard);
                }
            }
        }
    }

    private void OnAccessoryRemoved(EntityUid uid, UniformAccessoryHolderComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        // Only revert if the removed entity had a JobTitleChangerComponent and it matches the current override
        if (EntityManager.TryGetComponent(uid, out IdCardComponent? idCard))
        {
            // Check if the removed entity had a JobTitleChangerComponent
            if (EntityManager.TryGetComponent(args.Entity, out JobTitleChangerComponent? changer) &&
                idCard._jobTitle == changer.JobTitle)
            {
                // Try to get the mind's job name
                if (_minds.TryGetMind(uid, out var mindId, out var mind) &&
                    _jobs.MindTryGetJobName(mindId, out var jobName))
                {
                    idCard._jobTitle = jobName;
                }
                else
                {
                    idCard._jobTitle = null;
                }
                Dirty(uid, idCard);
            }
        }
    }
}
