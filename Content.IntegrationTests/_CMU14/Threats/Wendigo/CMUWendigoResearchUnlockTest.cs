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

    private static int Mh32Count(RequisitionsComputerComponent comp)
    {
        return comp.Categories
            .Where(category => category.Name == CMUWendigoResearchUnlockSystem.ResearchCategory)
            .SelectMany(category => category.Entries)
            .Count(entry => entry.Crate.Id == CMUWendigoResearchUnlockSystem.MH32Crate);
    }

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
                Assert.That(CorporateBalance(), Is.EqualTo(start + 500));

                research.UpdateClearance(0, 3, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 1000));

                research.UpdateClearance(7, -1, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 1000), "Credit-only updates must not pay out.");

                research.UpdateClearance(0, 3, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 1000), "An unchanged clearance must not pay out.");

                research.UpdateClearance(0, 1, "corporate");
                Assert.That(CorporateBalance(), Is.EqualTo(start + 1000), "A decrease must not pay out.");
            }
            finally
            {
                research.UpdateClearance(0, 1, "corporate");
                SEntMan.DeleteEntity(console);
            }
        });
    }

    [Test]
    public async Task CorporateClearanceThreeAddsMH32Once()
    {
        await Server.WaitAssertion(() =>
        {
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var console = SEntMan.SpawnEntity("CMUTestWendigoUnlockASRScorporate", MapCoordinates.Nullspace);
            try
            {
                research.UpdateClearance(0, 1, "corporate");
                var comp = SEntMan.GetComponent<RequisitionsComputerComponent>(console);
                Assert.That(Mh32Count(comp), Is.Zero);

                research.UpdateClearance(0, 2, "corporate");
                Assert.That(Mh32Count(comp), Is.Zero);

                research.UpdateClearance(0, 3, "corporate");
                Assert.That(Mh32Count(comp), Is.EqualTo(1));
                var entry = comp.Categories
                    .First(category => category.Name == CMUWendigoResearchUnlockSystem.ResearchCategory)
                    .Entries.Single(e => e.Crate.Id == CMUWendigoResearchUnlockSystem.MH32Crate);
                Assert.That(entry.Cost, Is.EqualTo(3500));
                Assert.That(entry.MaxStock, Is.EqualTo(1), "MH-32 is limited stock.");
                Assert.That(entry.StartingStock, Is.EqualTo(1));
                Assert.That(entry.StockReplenishDelay, Is.EqualTo(TimeSpan.FromMinutes(30)));

                research.UpdateClearance(0, 3, "corporate");
                research.UpdateClearance(0, 4, "corporate");
                Assert.That(Mh32Count(comp), Is.EqualTo(1));
            }
            finally
            {
                research.UpdateClearance(0, 1, "corporate");
                SEntMan.DeleteEntity(console);
            }
        });
    }

    [Test]
    public async Task ColonyNeverGetsMH32()
    {
        await Server.WaitAssertion(() =>
        {
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var console = SEntMan.SpawnEntity("CMUTestWendigoUnlockASRScolony", MapCoordinates.Nullspace);
            try
            {
                research.UpdateClearance(0, 1, "colony");
                research.UpdateClearance(0, 3, "colony");
                var comp = SEntMan.GetComponent<RequisitionsComputerComponent>(console);
                Assert.That(Mh32Count(comp), Is.Zero);

                var late = SEntMan.SpawnEntity("CMUTestWendigoUnlockASRScolony", MapCoordinates.Nullspace);
                Assert.That(Mh32Count(SEntMan.GetComponent<RequisitionsComputerComponent>(late)), Is.Zero);
                SEntMan.DeleteEntity(late);
            }
            finally
            {
                research.UpdateClearance(0, 1, "colony");
                SEntMan.DeleteEntity(console);
            }
        });
    }

    [Test]
    public async Task LateCorporateConsoleGetsMH32()
    {
        await Server.WaitAssertion(() =>
        {
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            EntityUid console = default;
            try
            {
                research.UpdateClearance(0, 3, "corporate");
                console = SEntMan.SpawnEntity("CMUTestWendigoUnlockASRScorporate", MapCoordinates.Nullspace);
                Assert.That(Mh32Count(SEntMan.GetComponent<RequisitionsComputerComponent>(console)), Is.EqualTo(1));
            }
            finally
            {
                research.UpdateClearance(0, 1, "corporate");
                if (console != default)
                    SEntMan.DeleteEntity(console);
            }
        });
    }

    [Test]
    public async Task WeylandYutaniFactionPredicate()
    {
        await Server.WaitAssertion(() =>
        {
            var unlock = SEntMan.System<CMUWendigoResearchUnlockSystem>();
            var platoons = SEntMan.System<PlatoonSpawnRuleSystem>();
            platoons.SelectedGovforPlatoon = null;
            platoons.SelectedOpforPlatoon = null;
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(unlock.IsWeylandYutaniFaction("corporate"), Is.True);
                    Assert.That(unlock.IsWeylandYutaniFaction("Corporate"), Is.True);
                    Assert.That(unlock.IsWeylandYutaniFaction("colony"), Is.False);
                    Assert.That(unlock.IsWeylandYutaniFaction("govfor"), Is.False);
                    Assert.That(unlock.IsWeylandYutaniFaction("opfor"), Is.False);
                    Assert.That(unlock.IsWeylandYutaniFaction(null), Is.False);
                });

                platoons.SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>("WEYU");
                Assert.That(unlock.IsWeylandYutaniFaction("GOVFOR"), Is.True);
                Assert.That(unlock.IsWeylandYutaniFaction("opfor"), Is.False);
            }
            finally
            {
                platoons.SelectedGovforPlatoon = null;
                platoons.SelectedOpforPlatoon = null;
            }
        });
    }
}
