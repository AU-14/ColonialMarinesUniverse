using Content.Server.Ghost;
using Content.Shared._RMC14.Xenonids.Construction.Nest;

namespace Content.Server._RMC14.Xenonids.Construction.Nest;

/// <summary>
/// Records a nested player's identity when the server handles their ghost attempt.
/// </summary>
public sealed class XenoNestedGhostSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostAttemptHandleEvent>(OnGhostAttempt);
    }

    private void OnGhostAttempt(GhostAttemptHandleEvent args)
    {
        if (args.Mind.CurrentEntity is not { } entity ||
            args.Mind.UserId is not { } userId ||
            !TryComp(entity, out XenoNestedComponent? nested))
        {
            return;
        }

        nested.GhostedId = userId;
        Dirty(entity, nested);
    }
}
