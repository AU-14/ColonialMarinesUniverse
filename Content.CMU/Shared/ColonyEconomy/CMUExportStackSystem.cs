using Content.Shared.Stacks;

namespace Content.Shared.CMU14.ColonyEconomy;

public sealed class CMUExportStackSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ASRSSellableComponent, CMUStackMergeAttemptEvent>(OnMergeAttempt);
        SubscribeLocalEvent<ASRSSellableComponent, StackSplitEvent>(OnSplit);
    }

    private void OnMergeAttempt(Entity<ASRSSellableComponent> stack, ref CMUStackMergeAttemptEvent args)
    {
        var other = args.Donor == stack.Owner ? args.Recipient : args.Donor;
        if (!HasComp<ASRSSellableComponent>(other))
            args.Cancelled = true;
    }

    private void OnSplit(Entity<ASRSSellableComponent> stack, ref StackSplitEvent args)
    {
        EnsureComp<ASRSSellableComponent>(args.NewId);
    }
}
