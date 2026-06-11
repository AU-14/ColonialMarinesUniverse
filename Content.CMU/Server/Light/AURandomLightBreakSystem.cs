using Content.Server.Light.EntitySystems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Light;

public sealed partial class AURandomLightBreakSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AURandomLightBreakComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, AURandomLightBreakComponent comp, MapInitEvent args)
    {
        var jitter = _random.NextFloat();
        comp.NextCheckAt = _timing.CurTime + comp.CheckInterval * jitter;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AURandomLightBreakComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextCheckAt)
                continue;

            comp.NextCheckAt = now + comp.CheckInterval;

            if (!_random.Prob(comp.BreakChance))
                continue;

            _poweredLight.TryDestroyBulb(uid);
        }
    }
}
