using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Roles;
using Content.Server.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Collections;

namespace Content.IntegrationTests.Tests.Roles;

[TestFixture]
public sealed class StartingGearPrototypeStorageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    /// <summary>
    /// Checks that a storage fill on a StartingGearPrototype will properly fill
    /// </summary>
    [Test]
    public async Task TestStartingGearStorage()
    {
        var pair = Pair;
        var server = pair.Server;
        var mapSystem = server.System<SharedMapSystem>();
        var storageSystem = server.System<StorageSystem>();

        var protos = server.ProtoMan
            .EnumeratePrototypes<StartingGearPrototype>()
            .Where(p => !p.Abstract)
            .ToList()
            .OrderBy(p => p.ID);

        var testMap = await pair.CreateTestMap();
        var coords = testMap.GridCoords;

        await server.WaitAssertion(() =>
        {
            foreach (var gearProto in protos)
            {
                var ents = new ValueList<EntityUid>();

                foreach (var (slot, entProtos) in gearProto.Storage)
                {
                    ents.Clear();
                    if (entProtos == null)
                        Assert.Fail($"StartingGearPrototype {gearProto.ID} has a null storage list for slot {slot}");

                    if (entProtos.Count == 0)
                        continue;

                    var storageProto = ((IEquipmentLoadout)gearProto).GetGear(slot);
                    if (storageProto == string.Empty)
                        continue;

                    var bag = server.EntMan.SpawnEntity(storageProto, coords);

                    foreach (var ent in entProtos)
                    {
                        ents.Add(server.EntMan.SpawnEntity(ent, coords));
                    }

                    foreach (var ent in ents)
                    {
                        if (!storageSystem.CanInsert(bag, ent, out _))
                        {
                            var entity = server.EntMan.GetComponent<MetaDataComponent>(ent).EntityPrototype?.ID ?? ent.ToString();
                            Assert.Fail($"StartingGearPrototype {gearProto.ID} could not insert {entity} into slot {slot} storage entity {storageProto} ({bag.Id})");
                        }

                        server.EntMan.DeleteEntity(ent);
                    }
                    server.EntMan.DeleteEntity(bag);
                }
            }

            mapSystem.DeleteMap(testMap.MapId);
        });
    }
}
