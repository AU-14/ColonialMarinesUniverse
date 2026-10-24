using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Crate;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Crate;

namespace Content.Server.CMU14.Crate;

/// <summary>
///     Ration crates aboard a faction's ship open full of that faction's platoon rations rather than UA MREs.
/// </summary>
public sealed partial class CMUPlatoonRationCrateSystem : EntitySystem
{
    private const string StandardRation = "CMMRE";

    [Dependency] private SharedDropshipSystem _dropship = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrateOpenableComponent, CMUCrateOpenableSpawnEvent>(OnSpawn);
    }

    private void OnSpawn(Entity<CrateOpenableComponent> ent, ref CMUCrateOpenableSpawnEvent args)
    {
        if (args.Prototype != StandardRation)
            return;

        var platoon = _dropship.GetCarrierFaction(ent) switch
        {
            "govfor" => _platoons.SelectedGovforPlatoon,
            "opfor" => _platoons.SelectedOpforPlatoon,
            _ => null,
        };

        if (platoon?.Rations is { } rations)
            args.Prototype = rations;
    }
}
