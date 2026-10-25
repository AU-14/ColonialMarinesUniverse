using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

// BUG-1055: faction squads kept the scavenger's leather satchel and brown shoes, and the UACG got USCM kit
[TestFixture, NonParallelizable]
public sealed class CMUExpeditionFactionOutfitTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true, ServerSeed = 1414 };

    [Test]
    public async Task FactionSquadsWearTheirOwnBootsAndPackAndKeepTheirSupplies()
    {
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out var map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            var lz = expedition.Plan.LandingZone;
            var origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));

            var agents = Server.System<CMUExpeditionAgentSystem>();
            var inventory = Server.System<InventorySystem>();
            foreach (var outfit in CMUExpeditionAgentSystem.OutfitNames.Where(name => name != "scavenger"))
            {
                foreach (var variant in new[] { "rifleman", "assault" })
                {
                    var spawned = agents.SpawnSquad(origin, 3, variant, out var squad, outfit);
                    Assert.That(spawned, Is.GreaterThan(0), $"{outfit} {variant}");
                    var query = SEntMan.EntityQueryEnumerator<CMUExpeditionAgentComponent>();
                    while (query.MoveNext(out var uid, out var agent))
                    {
                        if (agent.Squad != squad)
                            continue;

                        Assert.That(agent.Outfit, Is.EqualTo(outfit), $"{outfit} {variant} outfit was rejected");
                        Assert.That(inventory.TryGetSlotEntity(uid, "shoes", out var shoes), Is.True);
                        Assert.That(Proto(shoes!.Value), Is.Not.EqualTo("RMCShoesBrown"), $"{outfit} shoes");
                        Assert.That(inventory.TryGetSlotEntity(uid, "back", out var back), Is.True);
                        Assert.That(Proto(back!.Value), Is.Not.EqualTo("CMUExpeditionFieldPack"), $"{outfit} pack");
                        if (Proto(back.Value)!.StartsWith("CMUExpeditionFieldPack"))
                        {
                            Assert.That(SEntMan.GetComponent<StorageComponent>(back.Value).Container.ContainedEntities,
                                Is.Not.Empty, $"{outfit} supplies were lost with the old pack");
                        }

                        if (outfit != "uacg")
                            continue;

                        Assert.That(Carried(uid).Select(Proto), Has.None.EqualTo("RMCWeaponRifleM54CMK1"));
                        Assert.That(inventory.TryGetSlotEntity(uid, "outerClothing", out var armor), Is.True);
                        Assert.That(Proto(armor!.Value), Is.Not.EqualTo("AU14ArmorBallisticVest"));
                    }

                    var spawnedAgents = new List<EntityUid>();
                    var cleanup = SEntMan.EntityQueryEnumerator<CMUExpeditionAgentComponent>();
                    while (cleanup.MoveNext(out var uid, out _))
                        spawnedAgents.Add(uid);
                    foreach (var uid in spawnedAgents)
                        SEntMan.DeleteEntity(uid);
                }
            }

            SEntMan.DeleteEntity(map);
        });

        string? Proto(EntityUid uid) => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;

        IEnumerable<EntityUid> Carried(EntityUid uid)
        {
            var containers = Server.System<SharedContainerSystem>();
            var stack = new Stack<EntityUid>([uid]);
            while (stack.TryPop(out var current))
            {
                if (!SEntMan.TryGetComponent<ContainerManagerComponent>(current, out var manager))
                    continue;
                foreach (var container in containers.GetAllContainers(current, manager))
                {
                    foreach (var contained in container.ContainedEntities)
                    {
                        yield return contained;
                        stack.Push(contained);
                    }
                }
            }
        }
    }
}
