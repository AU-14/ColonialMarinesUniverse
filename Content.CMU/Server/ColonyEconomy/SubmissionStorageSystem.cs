using Content.Server.Storage.Components;
using Content.Shared.AU14.ColonyEconomy;
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

    private void OnEntityInserted(EntityUid uid, SubmissionStorageComponent storage, EntInsertedIntoContainerMessage args)
    {
        // Check if the storage's owner has a SubmissionStorageComponent
        if (!EntityManager.TryGetComponent(uid, out SubmissionStorageComponent? submission))
            return;

//I know this is so atrocious but I don't care right now
    //    EntityPrototype? proto = Prototype(uid);

      //  if (proto == null)
       //     return;
        Console.WriteLine("testtwo");


        _colonyBudget.AddToBudget(submission.RewardAmount);
        Console.WriteLine(_colonyBudget.GetBudget());
        Console.WriteLine("testthree");

        // Delete the submitted entity
        EntityManager.QueueDeleteEntity(args.Entity);
        Console.WriteLine("testfour");

    }
}
