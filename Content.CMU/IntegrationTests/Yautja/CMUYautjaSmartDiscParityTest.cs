using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaSmartDiscParityTest
{
    [Test]
    public async Task ActiveDiscSlashIgnoresArmourAndSwingsEveryTwoSeconds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            target = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var vest = entities.SpawnEntity("RMCArmorVest", map.GridCoords);
            Assert.That(entities.System<InventorySystem>().TryEquip(target, vest, "outerClothing", silent: true, force: true), Is.True);

            var disc = entities.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);
            var comp = entities.GetComponent<YautjaSmartDiscComponent>(disc);
            // take the dice out so the swing count is exact
            comp.LoseSwingChance = 0;
            comp.KnockdownChance = 0;

            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(hunter, disc), Is.True);
            Assert.That(entities.System<ItemToggleSystem>().TryActivate((disc, null), hunter), Is.True);
        });

        // reaches the target and gets one swing in, the next one waits for the 2 second delay
        await pair.RunSeconds(1.5f);

        await server.WaitAssertion(() =>
        {
            var slash = SlashOn(server.EntMan, target);
            Assert.That(slash, Is.InRange(15f, 25f),
                "one smartdisc slash is rand(15, 25) through attack_animal, which never checks armour");
        });

        await pair.RunSeconds(2f);

        await server.WaitAssertion(() =>
        {
            Assert.That(SlashOn(server.EntMan, target), Is.InRange(30f, 50f),
                "the second slash lands after the 2 second Life tick");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DiscWithNoPreySpinsOutAfterEightSeconds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        YautjaSmartDiscComponent comp = default!;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var disc = entities.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);
            comp = entities.GetComponent<YautjaSmartDiscComponent>(disc);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(hunter, disc), Is.True);
            Assert.That(entities.System<ItemToggleSystem>().TryActivate((disc, null), hunter), Is.True);
        });

        await pair.RunSeconds(6f);
        await server.WaitAssertion(() => Assert.That(comp.Active, Is.True));

        await pair.RunSeconds(3f);
        await server.WaitAssertion(() => Assert.That(comp.Active, Is.False,
            "cmss13 drops the disc once time_idle passes 3 Life ticks, long before its 16 second lifetime"));

        await pair.CleanReturnAsync();
    }

    private static float SlashOn(IEntityManager entities, EntityUid target)
    {
        var damage = entities.System<DamageableSystem>().GetAllDamage((target, null));
        return damage.DamageDict.TryGetValue("Slash", out var slash) ? slash.Float() : 0f;
    }
}
