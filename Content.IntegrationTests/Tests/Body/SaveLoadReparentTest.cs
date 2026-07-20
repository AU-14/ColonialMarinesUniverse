using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class SaveLoadReparentTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  name: HumanBodyDummy
  id: HumanBodyDummy
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: HumanBodyDummyOrgan

- type: entity
  id: HumanBodyDummyOrgan
  components:
  - type: Organ
";

    [Test]
    public async Task Test()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entities = server.ResolveDependency<IEntityManager>();
        var mapLoader = entities.System<MapLoaderSystem>();
        var containerSystem = entities.System<SharedContainerSystem>();
        var mapSystem = entities.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            mapSystem.CreateMap(out var mapId);
            mapSystem.CreateGridEntity(mapId);
            var human = entities.SpawnEntity("HumanBodyDummy", new MapCoordinates(0, 0, mapId));

            AssertBodyOrgans(human);

            var mapPath = new ResPath($"/{nameof(SaveLoadReparentTest)}{nameof(Test)}map.yml");

            Assert.That(mapLoader.TrySaveMap(mapId, mapPath));
            mapSystem.DeleteMap(mapId);

            Assert.That(mapLoader.TryLoadMap(mapPath, out var map, out _), Is.True);

            var loadedBodies = entities.EntityQueryEnumerator<BodyComponent>();
            EntityUid? loadedHuman = null;
            while (loadedBodies.MoveNext(out var uid, out _))
            {
                if (entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "HumanBodyDummy")
                {
                    loadedHuman = uid;
                    break;
                }
            }

            Assert.That(loadedHuman, Is.Not.Null);
            AssertBodyOrgans(loadedHuman.Value);
            entities.DeleteEntity(map);
        });

        await pair.CleanReturnAsync();

        return;

        void AssertBodyOrgans(EntityUid bodyUid)
        {
            var body = entities.GetComponent<BodyComponent>(bodyUid);
            Assert.That(body.Organs, Is.Not.Null);
            Assert.That(body.Organs!.ContainedEntities, Is.Not.Empty);

            var organContainer = containerSystem.GetContainer(bodyUid, BodyComponent.ContainerID);
            foreach (var organUid in body.Organs.ContainedEntities)
            {
                var organ = entities.GetComponent<OrganComponent>(organUid);
                Assert.Multiple(() =>
                {
                    Assert.That(organ.Body, Is.EqualTo(bodyUid));
                    Assert.That(organContainer.ContainedEntities, Does.Contain(organUid));
                });
            }
        }
    }
}
