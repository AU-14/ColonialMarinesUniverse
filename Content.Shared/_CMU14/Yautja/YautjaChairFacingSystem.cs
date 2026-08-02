using Content.Shared.Buckle.Components;
using Robust.Shared.Maths;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaChairFacingSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaChairFacingComponent, StrappedEvent>(OnStrapped);
    }

    private void OnStrapped(Entity<YautjaChairFacingComponent> chair, ref StrappedEvent args)
    {
        _transform.SetLocalRotation(args.Buckle.Owner, chair.Comp.Direction.ToAngle());
    }
}
