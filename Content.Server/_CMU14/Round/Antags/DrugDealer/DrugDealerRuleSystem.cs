using Content.Server.GameTicking.Rules;
using Content.Server.AU14.Systems;
using Robust.Shared.GameObjects;
using Content.Server._CMU14.Round.Antags.ColonyBounty;

namespace Content.Server._CMU14.Round.Antags.DrugDealer;

public sealed partial class DrugDealerRuleSystem : GameRuleSystem<DrugDealerRuleComponent>
{
    [Dependency] private WantedSystem _wantedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DrugDealerComponent, ComponentStartup>(OnDrugDealerSpawned);
    }

    private void OnDrugDealerSpawned(EntityUid uid, DrugDealerComponent component, ComponentStartup args)
        => _wantedSystem.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, "AUPaperDrugs");
}
