using Content.Server._RMC14.Shuttles;
using Content.Shared._RMC14.Evacuation;

namespace Content.Server.CMU14.Evacuation;

public sealed class CMUEvacuationBerthSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent args)
    {
        // Inherited lifeboat maps spawn invisible death floors after departure.
        // Vacated CMU berths use their actual floors and space instead of a lethal collision overlay.
        RemComp<RMCSpawnEntityOnFTLStartComponent>(args.Grid);
    }
}
