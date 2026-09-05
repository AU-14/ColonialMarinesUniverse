using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.ColonyEvacuation;

namespace Content.Server.CMU14.ColonyEvacuation;

public sealed partial class ColonyEvacuationSystem : EntitySystem
{
    [Dependency] private SharedEvacuationSystem _evacuation = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyWithdrawEvacEnabledEvent>(OnColonyWithdrawEvacEnabled);
    }

    private void OnColonyWithdrawEvacEnabled(ref ColonyWithdrawEvacEnabledEvent ev)
    {
        TriggerColonyEvacuation();
    }

    public void TriggerColonyEvacuation()
    {
        var query = EntityQueryEnumerator<RMCPlanetComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _evacuation.TriggerColonyEvacuation(uid);
            return;
        }
    }
}
