using Content.Server.GameTicking.Rules;
using Content.Server.AU14.Round.DrugDealer;
using Content.Server.AU14.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server.AU14.Round.DrugDealer;

public sealed class DrugDealerRuleSystem : GameRuleSystem<DrugDealerRuleComponent>
{
    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    // Add any special logic for the Drug Dealer antagonist here if needed

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DrugDealerRuleComponent, ComponentStartup>(OnDrugDealerSpawned);
    }

    private void OnDrugDealerSpawned(EntityUid uid, DrugDealerRuleComponent component, ref ComponentStartup args)
    {
        // Send CMB fax on drug dealer spawn
        _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperDrugs");
    }
}
