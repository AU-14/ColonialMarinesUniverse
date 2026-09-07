using System.Linq;
using Content.Shared.CMU14.Fishing.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Nutrition.Components;
using Content.Shared.Plunger.Components;
using Content.Shared.RatKing.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class ToiletFishingMergeRegressionTest
{
    private static readonly EntProtoId[] FishableToilets =
    [
        "ToiletEmpty",
        "ToiletGoldenEmpty",
    ];

    [Test]
    public async Task FishableToiletsKeepForkFishingWithoutMakingConstructedToiletsLootSources()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var id in FishableToilets)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
                Assert.That(proto!.TryComp<FixturesComponent>(out var fixtures, factory), Is.True, id.ToString());
                Assert.Multiple(() =>
                {
                    Assert.That(fixtures!.Fixtures, Does.ContainKey("fix1"), id.ToString());
                    Assert.That(fixtures.Fixtures, Does.ContainKey("fishing"), id.ToString());
                    Assert.That(proto.TryComp<FishingSpotComponent>(out var fishing, factory), Is.True, id.ToString());
                    Assert.That(fishing!.FishDefaultTimer, Is.EqualTo(45f), id.ToString());
                    Assert.That(fishing.FishTimerVariety, Is.EqualTo(15f), id.ToString());
                    Assert.That(proto.TryComp<PlungerUseComponent>(out _, factory), Is.True, id.ToString());
                    Assert.That(proto.TryComp<RummageableComponent>(out _, factory), Is.True, id.ToString());
                });
            }

            Assert.That(prototypes.TryIndex<EntityPrototype>("ConstructedToilet", out var constructed), Is.True);
            Assert.That(constructed!.TryComp<FixturesComponent>(out var constructedFixtures, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(constructedFixtures!.Fixtures, Does.ContainKey("fix1"));
                Assert.That(constructedFixtures.Fixtures, Does.Not.ContainKey("fishing"));
                Assert.That(constructed.TryComp<FishingSpotComponent>(out _, factory), Is.False);
                Assert.That(constructed.TryComp<PlungerUseComponent>(out _, factory), Is.False);
                Assert.That(constructed.TryComp<RummageableComponent>(out _, factory), Is.False);

                Assert.That(constructed.TryComp<SolutionManagerComponent>(out var solutions, factory), Is.True);
                Assert.That(solutions!.SolutionEnts!.Select(id => id.ToString()), Does.Contain("SolutionDrainNormal"));
                Assert.That(constructed.TryComp<SolutionRegenerationComponent>(out _, factory), Is.True);
                Assert.That(constructed.TryComp<EdibleComponent>(out var edible, factory), Is.True);
                Assert.That(edible!.Solution, Is.EqualTo("tank"));
                Assert.That(edible.DestroyOnEmpty, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
