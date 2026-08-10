using Content.Server._CMU14.Ops.ThirdParty;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Intel.Tech;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Intel;

public sealed partial class CMUFactionTechSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ThirdPartySystem _thirdParty = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TechPartySpawnEvent>(OnTechPartySpawn);
    }

    private void OnTechPartySpawn(TechPartySpawnEvent ev)
    {
        if (string.IsNullOrWhiteSpace(ev.ThirdPartyId) ||
            !_prototypes.TryIndex<ThirdPartyPrototype>(ev.ThirdPartyId, out var party))
        {
            Log.Warning($"Faction tech referenced missing third-party prototype '{ev.ThirdPartyId}'.");
            return;
        }

        if (!_prototypes.TryIndex<PartySpawnPrototype>(party.PartySpawn, out var spawn))
        {
            Log.Warning(
                $"Faction tech third party '{party.ID}' referenced missing party-spawn prototype '{party.PartySpawn}'.");
            return;
        }

        _thirdParty.SpawnThirdParty(party, spawn, false);
    }
}
