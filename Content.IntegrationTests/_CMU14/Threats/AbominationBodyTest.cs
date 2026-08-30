using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.CMU14.Threats.Mobs.Abomination;
using Content.Shared.Body;
using Content.Shared.Gibbing;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
[TestOf(typeof(AbominationDeathSystem))]
public sealed class AbominationBodyTest : GameTest
{
    private static readonly string[] Forms =
    [
        "AU14AbominationSkitter",
        "AU14AbominationGrunt",
        "AU14AbominationSpider",
        "AU14AbominationMimic",
    ];

    private static readonly string[] AnimalOrganCategories =
    [
        "Lungs",
        "Heart",
        "Stomach",
        "Liver",
        "Kidneys",
    ];

    [Test]
    [TestCaseSource(nameof(Forms))]
    public async Task FormUsesAnimalBodyAndGibsOnDeath(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid form = default;
        EntityUid[] organs = [];

        await Server.WaitAssertion(() =>
        {
            form = SEntMan.SpawnEntity(prototype, map.GridCoords);
            organs = AssertAnimalBody(form, prototype);

            Server.System<MobStateSystem>().ChangeMobState(form, MobState.Dead);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(form), Is.False, prototype);
            Assert.That(organs, Has.Length.EqualTo(AnimalOrganCategories.Length), prototype);

            foreach (var organ in organs)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.EntityExists(organ), Is.True, prototype);
                    Assert.That(SEntMan.GetComponent<OrganComponent>(organ).Body, Is.Null, prototype);
                    Assert.That(SEntMan.HasComponent<GibbableOrganComponent>(organ), Is.True, prototype);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(organ).MapID,
                        Is.EqualTo(map.MapId),
                        prototype);
                });
            }

            var kudzu = CountPrototypeOnMap("AU14AbominationFleshKudzu", map.MapId);
            Assert.That(kudzu, Is.EqualTo(1), prototype);
        });
    }

    private EntityUid[] AssertAnimalBody(EntityUid uid, string prototype)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<BodyComponent>(uid), Is.True, prototype);
            Assert.That(SEntMan.HasComponent<InitialBodyComponent>(uid), Is.False, prototype);
            Assert.That(SEntMan.HasComponent<VisualBodyComponent>(uid), Is.False, prototype);
            Assert.That(SEntMan.HasComponent<HumanoidProfileComponent>(uid), Is.False, prototype);
            Assert.That(SEntMan.HasComponent<HandsComponent>(uid), Is.False, prototype);
            Assert.That(SEntMan.HasComponent<InventoryComponent>(uid), Is.False, prototype);
        });

        var body = SEntMan.GetComponent<BodyComponent>(uid);
        Assert.That(body.Organs, Is.Not.Null, prototype);
        var organs = body.Organs!.ContainedEntities.ToArray();

        Assert.That(organs, Has.Length.EqualTo(AnimalOrganCategories.Length), prototype);
        var categories = new List<string>(organs.Length);
        foreach (var organ in organs)
        {
            var organComponent = SEntMan.GetComponent<OrganComponent>(organ);
            Assert.That(organComponent.Category, Is.Not.Null, prototype);
            categories.Add(organComponent.Category!.Value.Id);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<GibbableOrganComponent>(organ), Is.True, prototype);
                Assert.That(SEntMan.HasComponent<VisualOrganComponent>(organ), Is.False, prototype);
            });
        }

        Assert.That(categories, Is.EquivalentTo(AnimalOrganCategories), prototype);
        return organs;
    }

    private int CountPrototypeOnMap(EntProtoId prototype, MapId mapId)
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var metadata, out var transform))
        {
            if (!metadata.Deleted && metadata.EntityPrototype?.ID == prototype && transform.MapID == mapId)
                count++;
        }

        return count;
    }
}
