using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;

namespace Content.IntegrationTests.CMU14.Yautja;

// bug 820: a kitted pred shrugged off ~85% of rifle hits and took double xeno slash damage.
// the numbers are the cmss13 ones the reporter gave: mk1 ap does 8 to the chest, a warrior slash does 5
[TestFixture]
public sealed class YautjaArmorDamageNumbersTest
{
    [Test]
    public async Task ArmouredChestTakesCmss13RifleAndSlashDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var inventory = entMan.System<InventorySystem>();
            var hitLocation = entMan.System<SharedHitLocationSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var mesh = entMan.SpawnEntity("CMUYautjaBodyMesh", map.GridCoords);
            var armor = entMan.SpawnEntity("CMUYautjaClanArmor", map.GridCoords);
            Assert.That(inventory.TryEquip(hunter, mesh, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(hunter, armor, "outerClothing", silent: true, force: true), Is.True);

            var shooter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new(2, 0)));
            var bullet = entMan.SpawnEntity("BulletRifle10x24mmAP", map.GridCoords.Offset(new(1, 0)));
            var warrior = entMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new(-1, 0)));

            FixedPoint2 Hit(FixedPoint2 amount, string type, EntityUid origin, EntityUid tool)
            {
                hitLocation.SetForcedHit(hunter, BodyPartType.Torso);
                var before = damageable.GetTotalDamage(hunter);
                damageable.TryChangeDamage(hunter, new DamageSpecifier { DamageDict = { [type] = amount } },
                    origin: origin, tool: tool);
                return damageable.GetTotalDamage(hunter) - before;
            }

            // mk1 rifle: 30 piercing * 1.1 gun multiplier, 40 ap from the round itself
            var rifle = Hit(33, "Piercing", shooter, bullet);
            var slash = Hit(35, "Slash", warrior, warrior);

            Assert.Multiple(() =>
            {
                Assert.That(rifle.Float(), Is.EqualTo(8f).Within(0.75f), "Mk1 AP into a clan-armoured chest");
                Assert.That(slash.Float(), Is.EqualTo(5f).Within(0.75f), "Warrior slash into a clan-armoured chest");
            });
        });

        await pair.CleanReturnAsync();
    }
}
