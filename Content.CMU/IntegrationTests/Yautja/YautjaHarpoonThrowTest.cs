using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Hands.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHarpoonThrowTest : GameTest
{
    // ctrl+q aimed straight at the mob, which is how players throw
    [TestCase("CMUYautjaHarpoon", "CMMobHuman")]
    [TestCase("CMM11Knife", "CMMobHuman")]
    [TestCase("CMUYautjaHarpoon", "CMXenoDrone")]
    [TestCase("CMM11Knife", "CMXenoDrone")]
    public async Task ThrownWeaponHitsTheMobItWasAimedAt(string weapon, string victim)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid thrown = default;

        await Server.WaitAssertion(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            for (var x = 0; x < 8; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            var hunter = SEntMan.SpawnEntity("CMUMobYautja", mapSystem.GridTileToLocal(map.Grid, map.Grid, new Vector2i(0, 0)));
            target = SEntMan.SpawnEntity(victim, mapSystem.GridTileToLocal(map.Grid, map.Grid, new Vector2i(4, 0)));
            thrown = SEntMan.SpawnEntity(weapon, SEntMan.GetComponent<TransformComponent>(hunter).Coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(hunter, thrown), Is.True);

            var aim = SEntMan.GetComponent<TransformComponent>(target).Coordinates;
            Assert.That(SEntMan.System<HandsSystem>().ThrowHeldItem(hunter, aim), Is.True);
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(2));

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>().GetTotalDamage(target);
            var landed = SEntMan.GetComponent<TransformComponent>(thrown).LocalPosition.X;
            Assert.That(damage.Float(), Is.GreaterThan(0f), $"{weapon} landed at x {landed} without hurting the {victim}");
        });
    }
}
