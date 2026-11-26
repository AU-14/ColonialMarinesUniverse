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
        if (!TryComp<ASRSSellableComponent>(other, out var origin) || origin.FromOreDeposit != stack.Comp.FromOreDeposit)
            args.Cancelled = true;
    }

    private void OnSplit(Entity<ASRSSellableComponent> stack, ref StackSplitEvent args)
    {
        var origin = EnsureComp<ASRSSellableComponent>(args.NewId);
        origin.FromOreDeposit = stack.Comp.FromOreDeposit;
        Dirty(args.NewId, origin);
    }
}
