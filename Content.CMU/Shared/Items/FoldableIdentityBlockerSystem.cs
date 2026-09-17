using Content.Shared.Foldable;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;

namespace Content.Shared.CMU14.Items;

public sealed class FoldableIdentityBlockerSystem : EntitySystem
{
    [Dependency] private IdentitySystem _identity = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FoldableIdentityBlockerComponent, FoldedEvent>(OnFolded);
    }

    private void OnFolded(Entity<FoldableIdentityBlockerComponent> ent, ref FoldedEvent args)
    {
        if (!TryComp<IdentityBlockerComponent>(ent, out var blocker))
            return;

        blocker.Enabled = args.IsFolded;
        Dirty(ent, blocker);
        _identity.QueueIdentityUpdate(Transform(ent).ParentUid);
    }
}
