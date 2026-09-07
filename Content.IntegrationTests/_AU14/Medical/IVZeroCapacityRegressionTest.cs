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
  id: AU14ZeroCapacityBloodPack
  components:
  - type: BloodPack
  - type: Solution
    id: pack
    solution:
      maxVol: 0

- type: entity
  id: AU14HalfBloodPack
  components:
  - type: BloodPack
  - type: Solution
    id: pack
    solution:
      maxVol: 100
      reagents:
      - ReagentId: Water
        Quantity: 50

- type: entity
  id: AU14AutoSizedBloodPack
  components:
  - type: BloodPack
  - type: Solution
    id: pack
    solution:
      maxVol: 0
      reagents:
      - ReagentId: Water
        Quantity: 10

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
    public async Task ZeroCapacityBloodPackUsesCanonicalFillFraction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid zeroPack = default;
        EntityUid halfPack = default;
        EntityUid autoSizedPack = default;
        EntityUid iv = default;

        await server.WaitPost(() =>
        {
            zeroPack = server.EntMan.SpawnEntity("AU14ZeroCapacityBloodPack", MapCoordinates.Nullspace);
            halfPack = server.EntMan.SpawnEntity("AU14HalfBloodPack", MapCoordinates.Nullspace);
            autoSizedPack = server.EntMan.SpawnEntity("AU14AutoSizedBloodPack", MapCoordinates.Nullspace);
            iv = server.EntMan.SpawnEntity("AU14FillProjectionIV", MapCoordinates.Nullspace);

            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(iv, "pack", out var slot), Is.True);
            Assert.That(containers.Insert(zeroPack, slot), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var zeroBloodPack = server.EntMan.GetComponent<BloodPackComponent>(zeroPack);
            var zeroSolution = server.EntMan.GetComponent<SolutionComponent>(zeroPack).Solution;
            var halfBloodPack = server.EntMan.GetComponent<BloodPackComponent>(halfPack);
            var halfSolution = server.EntMan.GetComponent<SolutionComponent>(halfPack).Solution;
            var autoSizedBloodPack = server.EntMan.GetComponent<BloodPackComponent>(autoSizedPack);
            var autoSizedSolution = server.EntMan.GetComponent<SolutionComponent>(autoSizedPack).Solution;
            var ivDrip = server.EntMan.GetComponent<IVDripComponent>(iv);

            Assert.Multiple(() =>
            {
                Assert.That(zeroSolution.MaxVolume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(zeroSolution.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(zeroSolution.FillFraction, Is.EqualTo(1f));
                Assert.That(SharedSolutionContainerSystem.PercentFull(zeroSolution), Is.EqualTo(0f));
                Assert.That(zeroBloodPack.FillPercentage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(ivDrip.FillPercentage, Is.Zero);

                Assert.That(SharedSolutionContainerSystem.PercentFull(halfSolution), Is.EqualTo(50f));
                Assert.That(halfBloodPack.FillPercentage, Is.EqualTo(FixedPoint2.FromHundredths(50)));

                Assert.That(autoSizedSolution.MaxVolume, Is.EqualTo(autoSizedSolution.Volume));
                Assert.That(SharedSolutionContainerSystem.PercentFull(autoSizedSolution), Is.EqualTo(100f));
                Assert.That(autoSizedBloodPack.FillPercentage, Is.EqualTo(FixedPoint2.New(1)));
            });
        });

        await server.WaitPost(() =>
        {
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(iv, "pack", out var slot), Is.True);
            Assert.That(containers.Remove(zeroPack, slot), Is.True);
            Assert.That(containers.Insert(halfPack, slot), Is.True);
        });

        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.GetComponent<IVDripComponent>(iv).FillPercentage, Is.EqualTo(50)));

        await pair.CleanReturnAsync();
    }

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
