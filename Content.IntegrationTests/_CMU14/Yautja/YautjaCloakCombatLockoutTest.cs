using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaCloakCombatLockoutTest
{
    [Test]
    public async Task SuccessfulAttackBlocksRegularRecloak()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var target = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);

            var applied = entities.System<DamageableSystem>().TryChangeDamage(
                target,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } },
                origin: hunter,
                tool: hunter);
            Assert.That(applied?.AnyPositive(), Is.True);
            Assert.That(entities.GetComponent<YautjaBracerComponent>(bracer!.Value).CloakCombatLockoutUntil,
                Is.GreaterThan(TimeSpan.Zero));

            ToggleCloak(entities, hunter);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False);

            entities.GetComponent<YautjaBracerComponent>(bracer.Value).CloakCombatLockoutUntil = TimeSpan.Zero;
            ToggleCloak(entities, hunter);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
            entities.DeleteEntity(hunter);
            entities.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodDoesNotReceiveRegularCombatLockout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautjaBadBloodGrunt", map.GridCoords);
            var cloak = entities.System<YautjaCloakSystem>();
            cloak.ApplyOffensiveCombatLockout(hunter);

            Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
            Assert.That(entities.GetComponent<YautjaBracerComponent>(bracer!.Value).CloakCombatLockoutUntil,
                Is.EqualTo(TimeSpan.Zero));
            entities.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    private static void ToggleCloak(IEntityManager entities, EntityUid hunter)
    {
        Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
        var action = entities.System<SharedRMCActionsSystem>()
            .GetActionsWithEvent<YautjaToggleCloakActionEvent>(hunter).Single();
        var toggle = new YautjaToggleCloakActionEvent { Performer = hunter, Action = action };
        entities.EventBus.RaiseLocalEvent(bracer!.Value, toggle);
        Assert.That(toggle.Handled, Is.True);
    }
}
