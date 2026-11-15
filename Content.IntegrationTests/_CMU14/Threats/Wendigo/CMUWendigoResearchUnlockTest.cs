using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Research;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.util;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoResearchUnlockTest : GameTest
{


    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestWendigoUnlockASRScorporate
          parent: WYPMCCargoCatalog
          components:
          - type: RequisitionsComputer
            faction: corporate

        - type: entity
          id: CMUTestWendigoUnlockASRScolony
          parent: WYPMCCargoCatalog
          components:
          - type: RequisitionsComputer
            faction: colony

        """;

    private int CorporateBalance()
    {
        var query = SEntMan.EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (query.MoveNext(out _, out var account))
        {
            if (account.Faction == "corporate")
                return account.Balance;
        }

        Assert.Fail("No corporate requisitions account.");
        return 0;
    }

    [Test]
    public async Task ClearanceIncreaseCreditsCorporateAccount()
    {
        await Server.WaitAssertion(() =>
        {
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var console = SEntMan.SpawnEntity("CMUTestWendigoUnlockASRScorporate", MapCoordinates.Nullspace);
            try
            {
                research.UpdateClearance(0, 1, "corporate");
                var start = CorporateBalance();

                research.UpdateClearance(0, 2, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + CMUWendigoResearchUnlockSystem.ClearanceIncreaseReward));

                research.UpdateClearance(0, 3, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 2 * CMUWendigoResearchUnlockSystem.ClearanceIncreaseReward));

                research.UpdateClearance(7, -1, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 2 * CMUWendigoResearchUnlockSystem.ClearanceIncreaseReward), "Credit-only updates must not pay out.");

                research.UpdateClearance(0, 3, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 2 * CMUWendigoResearchUnlockSystem.ClearanceIncreaseReward), "An unchanged clearance must not pay out.");

                research.UpdateClearance(0, 1, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 2 * CMUWendigoResearchUnlockSystem.ClearanceIncreaseReward), "A decrease must not pay out.");
            }
            finally
            {
                research.UpdateClearance(0, 1, "corporate");
                SEntMan.DeleteEntity(console);
            }
        });
    }

}
