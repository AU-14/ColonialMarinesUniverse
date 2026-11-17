using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Medical.IV;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class IVZeroCapacityRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: AU14FillProjectionIV
  components:
  - type: IVDrip
  - type: ItemSlots
    slots:
      pack:
        name: pack
  - type: ContainerContainer
    containers:
      pack: !type:ContainerSlot {}

- type: entity
  parent: MobBloodstream
  id: AU14IVTransfusionRecipient
  components:
  - type: Dna
    dna: recipient-dna
  - type: Bloodstream
    bloodReferenceSolution:
      reagents:
      - ReagentId: Blood
        Quantity: 100
    bloodRefreshAmount: 0

- type: entity
  id: AU14IVTransfusionPack
  components:
  - type: BloodPack
  - type: Solution
    id: pack
    solution:
      maxVol: 100
""";

    private static readonly ProtoId<ReagentPrototype> Blood = "Blood";

    [Test]
    public async Task IvTransfusionNormalizesBloodToRecipientReference()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            var containers = entMan.System<SharedContainerSystem>();
            var adapter = entMan.System<SharedRMCBloodstreamSystem>();
            var bloodstreamSystem = entMan.System<BloodstreamSystem>();
            var ivSystem = entMan.System<IVDripSystem>();

            var recipient = entMan.SpawnEntity("AU14IVTransfusionRecipient", map.GridCoords);
            var pack = entMan.SpawnEntity("AU14IVTransfusionPack", map.GridCoords);
            var iv = entMan.SpawnEntity("AU14FillProjectionIV", map.GridCoords);

            var bloodstream = entMan.GetComponent<BloodstreamComponent>(recipient);
            Assert.That(solutions.TryGetSolution(
                recipient,
                bloodstream.BloodSolutionName,
                out var bloodSolutionEntity,
                out var bloodSolution), Is.True);
            Assert.That(solutions.TryGetSolution(pack, "pack", out var packSolutionEntity, out _), Is.True);

            List<ReagentData> recipientData = [new DnaData { DNA = "recipient-dna" }];
            bloodstreamSystem.ChangeBloodReagents(
                (recipient, bloodstream),
                new Solution(Blood, FixedPoint2.New(100), recipientData));
            var recipientBlood = bloodSolution.Contents.Single(entry => entry.Reagent.Prototype == Blood).Reagent;
            solutions.RemoveReagent(bloodSolutionEntity!.Value, recipientBlood, FixedPoint2.New(5));
            solutions.AddSolution(
                packSolutionEntity!.Value,
                new Solution(Blood, FixedPoint2.New(5), [new DnaData { DNA = "donor-dna" }]));

            Assert.That(bloodstreamSystem.GetBloodLevel((recipient, bloodstream)), Is.EqualTo(0.95f).Within(0.001f));

            Assert.That(containers.TryGetContainer(iv, "pack", out var slot), Is.True);
            Assert.That(containers.Insert(pack, slot), Is.True);

            var ivDrip = entMan.GetComponent<IVDripComponent>(iv);
            ivDrip.AttachedTo = recipient;
            ivDrip.TransferAt = TimeSpan.Zero;
            ivSystem.Update(0f);

            Assert.That(adapter.TryGetChemicalSolution(recipient, out _, out var chemicals), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(bloodstreamSystem.GetBloodLevel((recipient, bloodstream)), Is.EqualTo(1f).Within(0.001f),
                    "The IV did not replenish the recipient's blood level.");
                Assert.That(bloodSolution.Contents.Count(entry => entry.Reagent.Prototype == Blood), Is.EqualTo(1),
                    "The IV left donor blood as a separate reagent that can be metabolized as poison.");
                Assert.That(chemicals!.GetTotalPrototypeQuantity(Blood), Is.EqualTo(FixedPoint2.Zero),
                    "The IV exposed donor blood as a foreign bloodstream chemical.");
            });
        });

        await pair.CleanReturnAsync();
    }
}
