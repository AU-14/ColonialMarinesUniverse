using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Storage; // CMU14
using Content.Shared.Item; // CMU14
using Content.Shared.Roles;
using Content.Shared.Storage; // CMU14
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
            .Where(p => !pair.IsTestPrototype(p))
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
                        // CMU14: Runtime gear fills raise this so RMC storage can expand
                        // its grid. Without it a pre-filled bag fails CanInsert in the test.
                        if (server.EntMan.TryGetComponent<ItemComponent>(ent, out var item))
                        {
                            var storage = server.EntMan.GetComponent<StorageComponent>(bag);
                            var ev = new CMStorageItemFillEvent((ent, item), storage);
                            server.EntMan.EventBus.RaiseLocalEvent(bag, ref ev);
                        }

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
