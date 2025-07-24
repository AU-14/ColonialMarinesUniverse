using Content.Server.Storage.Components;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class SubmissionStorageSystem : EntitySystem
{
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SubmissionStorageComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
    }

    private void OnEntityInserted(EntityUid uid,
        SubmissionStorageComponent storage,
        EntInsertedIntoContainerMessage args)
    {
        if (!EntityManager.TryGetComponent(uid, out SubmissionStorageComponent? submission))
            return;

        int stackCount = 1;
        if (EntityManager.TryGetComponent<StackComponent>(args.Entity, out var stack))
        {
            stackCount = stack.Count;
            _colonyBudget.AddToBudget(submission.RewardAmount * stackCount);

            EntityManager.QueueDeleteEntity(args.Entity);

        }

        else
        {
            _colonyBudget.AddToBudget(submission.RewardAmount);

            EntityManager.QueueDeleteEntity(args.Entity);

        }


    }
    }

