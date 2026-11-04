using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A jeep's wreck stays where it burnt out: blasts, fire, shots and acid leave it, and only a welder
/// cuts it down, into scrap.
/// </summary>
[TestFixture]
public sealed class CMUJeepWreckTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Wreck)> SpawnWreck()
    {
        var map = await Pair.CreateTestMap();
        EntityUid wreck = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            wreck = SEntMan.SpawnEntity("CMUJeepWreckCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return (map, wreck);
    }

    [Test]
    public async Task NothingDestroysTheWreck()
    {
        var (_, wreck) = await SpawnWreck();
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.TryGetComponent(wreck, out CorrodibleComponent? corrodible) && corrodible.IsCorrodible, Is.False,
                "Acid shouldn't eat the wreck.");

            var damage = new DamageSpecifier();
            foreach (var type in new[] { "Blunt", "Piercing", "Heat", "Structural" })
                damage.DamageDict[type] = 10000;
            SEntMan.System<DamageableSystem>().TryChangeDamage(wreck, damage);

            // A big blast right on it, far past a rocket.
            SEntMan.System<ExplosionSystem>().QueueExplosion(SEntMan.System<SharedTransformSystem>().GetMapCoordinates(wreck),
                "RMC", 2000f, 4f, 200f, cause: null, maxTileBreak: 0, canCreateVacuum: false);
        });
        await RunSeconds(2f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.Deleted(wreck), Is.False, "The wreck should outlast anything thrown at it."));
    }

    [Test]
    public async Task AWelderCutsItDownToScrap()
    {
        var (map, wreck) = await SpawnWreck();
        await Server.WaitPost(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            var welder = SEntMan.SpawnEntity("CMWelder", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, welder), Is.True);
            Assert.That(SEntMan.System<ItemToggleSystem>().TryActivate(welder, user), Is.True, "The welder should light.");
            SEntMan.EventBus.RaiseLocalEvent(wreck,
                new InteractUsingEvent(user, welder, wreck, SEntMan.GetComponent<TransformComponent>(wreck).Coordinates));
        });
        await RunSeconds(17f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(wreck), Is.True, "The welder should cut the wreck down.");
            var scrap = SEntMan.EntityQuery<MetaDataComponent>()
                .Select(m => m.EntityPrototype?.ID)
                .Where(id => id is "CMSheetMetal10" or "CMRodMetal10")
                .ToList();
            Assert.That(scrap, Does.Contain("CMSheetMetal10").And.Contain("CMRodMetal10"), "It leaves metal sheets and rods.");
        });
    }
}
