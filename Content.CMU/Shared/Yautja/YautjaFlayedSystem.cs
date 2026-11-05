using Content.Shared._RMC14.Xenonids;
using Content.Shared.Examine;
using Content.Shared.Stacks;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaFlayedSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaFlayedComponent, ExaminedEvent>(OnFlayedExamined);
        SubscribeLocalEvent<YautjaFlayedHideComponent, CMUStackMergeAttemptEvent>(OnHideMergeAttempt);
        SubscribeLocalEvent<YautjaFlayedHideComponent, StackSplitEvent>(OnHideSplit);
    }

    // cmss13 MOB_FLAYED examine, xenos get their own line like they do there
    private void OnFlayedExamined(Entity<YautjaFlayedComponent> ent, ref ExaminedEvent args)
    {
        if (!IsFullyFlayed(ent.Comp))
            return;

        if (HasComp<XenoComponent>(args.Examiner))
        {
            args.PushMarkup(Loc.GetString("cmu-yautja-flayed-examine-xeno"), 10);
            return;
        }

        using (args.PushGroup(nameof(YautjaFlayedSystem), 10))
        {
            args.PushMarkup(Loc.GetString("cmu-yautja-flayed-examine-face", ("target", ent.Owner)));
            args.PushMarkup(Loc.GetString("cmu-yautja-flayed-examine-skin", ("target", ent.Owner)));
        }
    }

    public static bool IsFullyFlayed(YautjaFlayedComponent comp)
    {
        return comp.Stage >= 3 && comp.NextStage == YautjaFlayingStage.Complete;
    }

    // a stack with no marker counts as nobody's skin, same as cmss13's default stack_id
    private void OnHideMergeAttempt(Entity<YautjaFlayedHideComponent> ent, ref CMUStackMergeAttemptEvent args)
    {
        var other = args.Donor == ent.Owner ? args.Recipient : args.Donor;
        var otherVictim = CompOrNull<YautjaFlayedHideComponent>(other)?.Victim ?? string.Empty;
        if (otherVictim != ent.Comp.Victim)
            args.Cancelled = true;
    }

    // split spawns a fresh prototype, so carry the victim over or it'd merge with anyone's skin again
    private void OnHideSplit(Entity<YautjaFlayedHideComponent> ent, ref StackSplitEvent args)
    {
        var split = EnsureComp<YautjaFlayedHideComponent>(args.NewId);
        split.Victim = ent.Comp.Victim;
        Dirty(args.NewId, split);
        _meta.SetEntityName(args.NewId, Name(ent));
    }
}
