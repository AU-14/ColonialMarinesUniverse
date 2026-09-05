using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Xenomorphs.Larva;

public sealed partial class BloodyLarvaSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private INetManager _net = default!;

    public void SetBloody(EntityUid larva)
    {
        var comp = EnsureComp<BloodyLarvaComponent>(larva);
        comp.RemoveAt = _timing.CurTime + comp.Duration;
        Dirty(larva, comp);
        _appearance.SetData(larva, BloodyLarvaVisuals.Bloody, true);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<BloodyLarvaComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.RemoveAt)
                continue;

            _appearance.SetData(uid, BloodyLarvaVisuals.Bloody, false);
            RemCompDeferred<BloodyLarvaComponent>(uid);
        }
    }
}