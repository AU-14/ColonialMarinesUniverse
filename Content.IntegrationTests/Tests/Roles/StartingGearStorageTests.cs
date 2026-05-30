using System.Linq;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Roles;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Roles;

[TestFixture]
public sealed class StartingGearPrototypeStorageTest
{
    /// <summary>
    /// Checks that a storage fill on a StartingGearPrototype will properly fill
    /// </summary>
    [Test]
    public async Task TestStartingGearStorage()
    {
        var settings = new PoolSettings { Connected = true, Dirty = true };
        await using var pair = await PoolManager.GetServerClient(settings);
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
                        if (!storageSystem.CanInsert(bag, ent, null, out _))
                            Assert.Fail($"StartingGearPrototype {gearProto.ID} could not insert {server.EntMan.GetComponent<MetaDataComponent>(ent).EntityPrototype?.ID ?? ent.ToString()} into slot {slot} storage entity {storageProto} ({bag.Id})");

                        server.EntMan.DeleteEntity(ent);
                    }
                    server.EntMan.DeleteEntity(bag);
                }
            }

            mapSystem.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }
}
