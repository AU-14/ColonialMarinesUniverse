using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reaction;

namespace Content.Shared._RMC14.Chemistry;

public sealed class RMCSilverCatalystContainerSystem : EntitySystem
{
    private const string FormaldehydeSilverBeakerReaction = "RMCFormaldehydeSilverBeakerReaction";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReactionAttemptEvent>(OnReactionAttempt);
    }

    private void OnReactionAttempt(ref ReactionAttemptEvent args)
    {
        if (args.Cancelled || args.Reaction.ID != FormaldehydeSilverBeakerReaction)
            return;

        if (HasComp<RMCSilverCatalystContainerComponent>(args.Solution.Owner))
            return;

        if (TryComp<ContainedSolutionComponent>(args.Solution.Owner, out var contained) &&
            HasComp<RMCSilverCatalystContainerComponent>(contained.Container))
        {
            return;
        }

        args.Cancelled = true;
    }
}
