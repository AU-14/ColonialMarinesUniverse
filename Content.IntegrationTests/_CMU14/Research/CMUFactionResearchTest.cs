using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Research;

[TestFixture]
public sealed class CMUFactionResearchTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

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

}
