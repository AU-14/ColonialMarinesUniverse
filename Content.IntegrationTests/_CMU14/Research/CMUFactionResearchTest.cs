using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Research;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Projectiles;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Research;

[TestFixture]
public sealed class CMUFactionResearchTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestResearchASRSgovfor
          parent: WYPMCCargoCatalog
          components:
          - type: RequisitionsComputer
            faction: govfor

        - type: entity
          id: CMUTestResearchASRSopfor
          parent: WYPMCCargoCatalog
          components:
          - type: RequisitionsComputer
            faction: opfor

        - type: entity
          id: CMUTestResearchASRScolony
          parent: WYPMCCargoCatalog
          components:
          - type: RequisitionsComputer
            faction: colony

        - type: entity
          id: CMUTestResearchASRScorporate
          parent: WYPMCCargoCatalog
          components:
          - type: RequisitionsComputer
            faction: corporate

        """;

    [Test]
    public async Task CooldownPurchasesAreIsolatedAndValidated()
    {
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ServerResearchDataTerminalSystem>();
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            var gov = system.GetResearch("govfor");
            var op = system.GetResearch("opfor");
            var oldCount = system.ResearchChemAmount;
            try
            {
                system.UpdateClearance(2, 3, "govfor");
                system.UpdateClearance(9, 2, "opfor");
                gov.Picked = true;
                gov.NextReroll = now + TimeSpan.FromSeconds(90);
                op.Picked = true;
                op.NextReroll = now + TimeSpan.FromSeconds(360);
                Assert.That(system.ReduceCooldown("govfor"), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(gov.NextReroll, Is.EqualTo(now + TimeSpan.FromSeconds(30)));
                    Assert.That(system.GetCredits("govfor"), Is.EqualTo(1));
                    Assert.That(system.GetClearance("govfor"), Is.EqualTo(3));
                    Assert.That(op.NextReroll, Is.EqualTo(now + TimeSpan.FromSeconds(360)));
                    Assert.That(system.GetCredits("opfor"), Is.EqualTo(9));
                    Assert.That(system.GetClearance("opfor"), Is.EqualTo(2));
                    Assert.That(gov.Reports, Is.Not.SameAs(op.Reports));
                    Assert.That(gov.Selectable, Is.Not.SameAs(op.Selectable));
                });

                // Isolate timer completion from random chemical generation.
                system.ResearchChemAmount = 0;
                Assert.That(system.ReduceCooldown("govfor"), Is.True);
                Assert.That(gov.Picked, Is.False);
                Assert.That(gov.NextReroll, Is.EqualTo(now + system.RerollTime));
                Assert.That(system.GetCredits("govfor"), Is.Zero);
                Assert.That(system.ReduceCooldown("govfor"), Is.False);

                gov.Picked = true;
                Assert.That(system.ReduceCooldown("govfor"), Is.False, "No points must not buy time.");
                system.UpdateClearance(2, -1, "govfor");
                gov.NextReroll = now;
                Assert.That(system.ReduceCooldown("govfor"), Is.False, "Expired waits must not charge points.");
                gov.Picked = false;
                gov.NextReroll = now + TimeSpan.FromSeconds(90);
                Assert.That(system.ReduceCooldown("govfor"), Is.False, "Unpicked offers must not charge points.");
                Assert.That(system.GetCredits("govfor"), Is.EqualTo(2));

                system.PickChem("invalid-contract-id");
                Assert.That(system.GetResearch("corporate").Picked, Is.False);
            }
            finally
            {
                system.ResearchChemAmount = oldCount;
                system.UpdateClearance(0, 1, "govfor");
                system.UpdateClearance(0, 1, "opfor");
                gov.Picked = op.Picked = false;
                gov.NextReroll = op.NextReroll = TimeSpan.Zero;
            }
        });
    }

    [Test]
    public async Task AcceptingContractLocksEveryTerminalInFaction()
    {
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ServerResearchDataTerminalSystem>();
            var gov = system.GetResearch("govfor");
            var op = system.GetResearch("opfor");
            gov.Selectable.Add(new GeneratedReagentData { ID = "gov-contract-one" });
            gov.Selectable.Add(new GeneratedReagentData { ID = "gov-contract-two" });
            op.Selectable.Add(new GeneratedReagentData { ID = "op-contract" });

            try
            {
                Assert.That(system.TryReserveContract("govfor", "gov-contract-one", out var accepted), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(accepted.ID, Is.EqualTo("gov-contract-one"));
                    Assert.That(gov.Picked, Is.True);
                    Assert.That(system.TryReserveContract("govfor", "gov-contract-two", out _), Is.False,
                        "A stale window from another GOVFOR terminal must not accept another contract.");
                    Assert.That(system.TryReserveContract("opfor", "op-contract", out _), Is.True,
                        "A different faction's contract pool must remain available.");
                });
            }
            finally
            {
                gov.Selectable.Clear();
                op.Selectable.Clear();
                gov.Picked = op.Picked = false;
                gov.NextReroll = op.NextReroll = TimeSpan.Zero;
            }
        });
    }

    [Test]
    public async Task AsrsRefreshKeepsOnlyTheMatchingTerminal()
    {
        await Server.WaitAssertion(() =>
        {
            var requisitions = SEntMan.System<RequisitionsSystem>();
            var consoles = new List<(EntityUid Uid, string Crate)>();
            try
            {
                foreach (var (faction, suffix) in new[]
                {
                    ("govfor", "Govfor"), ("opfor", "Opfor"), ("colony", "Colony"), ("corporate", ""),
                })
                {
                    var uid = SEntMan.SpawnEntity("CMUTestResearchASRS" + faction, MapCoordinates.Nullspace);
                    consoles.Add((uid, "CMUCrateResearchTerminal" + suffix));
                }

                requisitions.ReapplyPlatoonCatalogs();
                requisitions.ReapplyPlatoonCatalogs();
                foreach (var (uid, crate) in consoles)
                {
                    var computer = SEntMan.GetComponent<RequisitionsComputerComponent>(uid);
                    var terminals = computer.Categories.SelectMany(category => category.Entries)
                        .Where(entry => entry.Crate.Id.StartsWith("CMUCrateResearchTerminal")).ToList();
                    Assert.That(terminals.Select(entry => entry.Crate.Id), Is.EqualTo(new[] { crate }));
                    Assert.That(computer.ItemCatalog.Any(entry => entry.Prototype.Id == crate), Is.True);
                }
            }
            finally
            {
                foreach (var (uid, _) in consoles)
                    SEntMan.DeleteEntity(uid);
            }
        });
    }

    [TestCase("Govfor", "govfor", "CMUCrateResearchTerminalGovfor")]
    [TestCase("Opfor", "opfor", "CMUCrateResearchTerminalOpfor")]
    [TestCase("Colony", "colony", "CMUCrateResearchTerminalColony")]
    [TestCase("Corporate", "corporate", "CMUCrateResearchTerminal")]
    public async Task ResearchCratesSupplyTheirFaction(string suffix, string faction, string crateId)
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var terminalId = "CMUResearchDataTerminal" + suffix;
            var terminal = prototypes.Index<EntityPrototype>(terminalId);
            var crate = prototypes.Index<EntityPrototype>(crateId);
            Assert.That(((ResearchDataTerminalComponent) terminal.Components["ResearchDataTerminal"].Component).Faction,
                Is.EqualTo(faction));
            Assert.That(((SpawnOnTerminateComponent) crate.Components["SpawnOnTerminate"].Component).Spawn.Id,
                Is.EqualTo(terminalId));
        });
    }
}
