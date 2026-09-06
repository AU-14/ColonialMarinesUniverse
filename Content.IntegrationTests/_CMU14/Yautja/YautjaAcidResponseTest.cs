using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Slowing;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaAcidResponseTest
{
    [Test]
    public async Task DamageDecloaksAndActiveDamageOverTimeBlocksRecloaking()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid hunter = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            hunter = entities.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            ToggleCloak(entities, hunter);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);

            entities.EnsureComponent<UserAcidedComponent>(hunter);
            var damage = entities.System<DamageableSystem>().TryChangeDamage(hunter,
                new DamageSpecifier { DamageDict = { ["Caustic"] = 1 } }, ignoreResistances: true);
            Assert.That(damage?.AnyPositive(), Is.True);
            Assert.That(entities.GetComponent<EntityTurnInvisibleComponent>(hunter).Enabled, Is.False,
                "the first positive acid tick must reveal the hunter");
        });
        await pair.RunTicksSync(1);

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False);
            ToggleCloak(entities, hunter);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False,
                "active acid must prevent recloaking");

            var cloak = entities.System<YautjaCloakSystem>();
            Assert.That(cloak.GetDamageOverTimeBlocker(hunter), Is.EqualTo(YautjaCloakDotBlocker.Acid));
            entities.RemoveComponent<UserAcidedComponent>(hunter);

            var flammable = entities.GetComponent<FlammableComponent>(hunter);
            flammable.OnFire = true;
            Assert.That(cloak.GetDamageOverTimeBlocker(hunter), Is.EqualTo(YautjaCloakDotBlocker.Fire));
            flammable.OnFire = false;

            entities.EnsureComponent<UserDamageOverTimeComponent>(hunter);
            Assert.That(cloak.GetDamageOverTimeBlocker(hunter), Is.EqualTo(YautjaCloakDotBlocker.Other));
            entities.RemoveComponent<UserDamageOverTimeComponent>(hunter);

            entities.DeleteEntity(hunter);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AcidMovementImmunityExcludesBadBlood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var regular = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var badBlood = entities.SpawnEntity("CMUMobYautjaBadBloodGrunt", map.GridCoords);
            var policy = entities.System<YautjaAcidResponseSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(policy.ShouldSkipAcidMoveEffects(regular), Is.True);
                Assert.That(policy.ShouldSkipAcidMoveEffects(badBlood), Is.False);
            });

            var acid = entities.SpawnEntity("XenoBombardAcidProjectile", map.GridCoords);
            var acidHit = new ProjectileHitEvent(new DamageSpecifier(), regular);
            entities.EventBus.RaiseLocalEvent(acid, ref acidHit);
            Assert.That(entities.System<StandingStateSystem>().IsDown(regular), Is.False,
                "an explicitly acid-tagged slowing projectile must not paralyze a regular Yautja");

            var neuro = entities.SpawnEntity("XenoQueenNeuroSpitProjectile", map.GridCoords);
            var neuroHit = new ProjectileHitEvent(new DamageSpecifier(), regular);
            entities.EventBus.RaiseLocalEvent(neuro, ref neuroHit);
            Assert.That(entities.System<StandingStateSystem>().IsDown(regular), Is.True,
                "neurotoxin must retain its normal movement effect");
            entities.DeleteEntity(regular);
            entities.DeleteEntity(badBlood);
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
