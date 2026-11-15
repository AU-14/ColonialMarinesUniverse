using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.Mind;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Medical.Anatomy.BodyParts;

/// <summary>
/// CMU14: upstream BrainSystem moves the mind into the brain whenever the brain leaves a body, so a decapitated
/// player ended up "alive" inside a loose head: no mob state, so LOOC and the rest still worked. cmss13 keeps a
/// decapitated human in their own (dead) body, so a severed part that carried the mind off hands it straight back./// </summary>
public sealed partial class CMUSeveredPartMindSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private const int MaxDepth = 6;

    public override void Initialize()
    {
        SubscribeLocalEvent<BodyPartSeveredEvent>(OnSevered);
    }

    private void OnSevered(ref BodyPartSeveredEvent args)
    {
        if (TerminatingOrDeleted(args.Body) || _mind.TryGetMind(args.Body, out _, out _))
            return;

        // The detached part sits inside a DetachedBody wrapper, and that wrapper is what the brain hands the mind to.
        var root = args.Part;
        for (var i = 0; i < MaxDepth && _container.TryGetContainingContainer((root, null, null), out var container) &&
                        container.Owner != args.Body; i++)
        {
            root = container.Owner;
        }

        if (!TryFindMind(root, 0, out var mindId, out var mind))
            return;

        _mind.TransferTo(mindId, args.Body, mind: mind);
    }

    private bool TryFindMind(EntityUid uid, int depth, out EntityUid mindId, out MindComponent mind)
    {
        if (_mind.TryGetMind(uid, out mindId, out mind!))
            return true;

        if (depth >= MaxDepth || !TryComp(uid, out ContainerManagerComponent? containers))
            return false;

        foreach (var container in containers.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryFindMind(contained, depth + 1, out mindId, out mind))
                    return true;
            }
        }

        return false;
    }
}
