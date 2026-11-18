using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Vehicle;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.CMU14.util;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUInsurgencyTankTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Destructive = true };

    [Test]
    public async Task TierTwoPurchaseAddsTankToDepotWhileAircraftStayUnavailable()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!
                .SetValue(Server.System<GameTicker>(), SProtoMan.Index<GamePresetPrototype>("Insurgency"));
            Server.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>("USCM");
            var console = SEntMan.SpawnEntity("VehicleSupplyConsole", map.GridCoords);
            var depot = SEntMan.GetComponent<VehicleSupplyConsoleComponent>(console);
            depot.Faction = "govfor";
            var supply = Server.System<VehicleSupplySystem>();
            var initial = supply.GetCatalog(console, depot).Select(entry => entry.Vehicle.Id).ToList();
            Assert.That(initial, Does.Contain("VehicleHumvee"));
            Assert.That(initial, Does.Not.Contain("VehicleTank"));
            var tree = Server.System<IntelSystem>().EnsureTechTree("govfor").Comp.Tree;
            var index = tree.Options[2].FindIndex(option => option.Name == "cmu-tech-insurgency-tank");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));

            var research = SEntMan.SpawnEntity("RMCTechTreeConsoleGovfor", map.GridCoords);
            var buyer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            tree.Tier = 2;
            tree.Points = tree.Options[2][index].CurrentCost;
            SEntMan.EventBus.RaiseLocalEvent(research, new TechPurchaseOptionBuiMsg(2, index)
            {
                Team = "govfor", Actor = buyer, UiKey = TechControlConsoleUI.Key,
            });
            var after = supply.GetCatalog(console, depot).Select(entry => entry.Vehicle.Id).ToList();
            Assert.That(after, Does.Contain("VehicleTank"));
            Assert.That(after, Does.Not.Contain("CMUFighterGround"));
            Assert.That(after, Does.Not.Contain("VehicleBlackfootTransport"));
        });
    }
}
