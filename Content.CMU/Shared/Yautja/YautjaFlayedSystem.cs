using Content.Shared._RMC14.Xenonids;
using Content.Shared.Examine;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaFlayedSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaFlayedComponent, ExaminedEvent>(OnFlayedExamined);
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
}
