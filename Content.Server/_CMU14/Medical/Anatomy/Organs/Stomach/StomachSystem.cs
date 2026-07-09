using Content.Server.Medical;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Stomach;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Anatomy.Organs.Stomach;

public sealed partial class StomachSystem : SharedStomachSystem
{
    [Dependency] private VomitSystem _vomit = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

    protected override void ApplyVomit(EntityUid body)
    {
        _vomit.Vomit(body, -20f, -20f);
    }
}
