using Content.Shared._RMC14.Medical.IV;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.IntegrationTests._AU14.Medical;

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
  - type: ContainerContainer
    containers:
      pack: !type:ContainerSlot {}
""";

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
}
