using Content.Server.Medical;
using Content.Shared._CMU14.Medical.Organs.Stomach;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Organs.Stomach;

public sealed class StomachSystem : SharedStomachSystem
{
    [Dependency] private readonly VomitSystem _vomit = default!;

    protected override void ApplyVomit(EntityUid body)
    {
        _vomit.Vomit(body, -20f, -20f);
    }
}
