using Content.Server.GameTicking;
using Content.Server.Wires;
using Content.Shared._RMC14.Rules;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Airlock;

public sealed partial class AURandomAirlockBreakSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AURandomAirlockBreakComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, AURandomAirlockBreakComponent comp, MapInitEvent args)
    {
        var jitter = _random.NextFloat();
        comp.NextCheckAt = _timing.CurTime + comp.CheckInterval * jitter;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.IsGameRuleActive<CMDistressSignalRuleComponent>())
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AURandomAirlockBreakComponent, WiresComponent>();
        while (query.MoveNext(out var uid, out var comp, out var wires))
        {
            if (now < comp.NextCheckAt)
                continue;

            comp.NextCheckAt = now + comp.CheckInterval;

            if (!_random.Prob(comp.BreakChance))
                continue;

            var actionWires = wires.WiresList
                .FindAll(w => w.Action != null && !w.IsCut);

            if (actionWires.Count == 0)
                continue;

            var target = _random.Pick(actionWires);
            if (target.Action!.Cut(uid, target))
                target.IsCut = true;
        }
    }
}
