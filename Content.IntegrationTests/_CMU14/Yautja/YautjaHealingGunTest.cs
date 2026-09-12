using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14 Test: Yautja healing-gun cooldown and autonomous repair behavior.
[TestFixture]
[TestOf(typeof(YautjaHealingGunComponent))]
public sealed class YautjaHealingGunTest : GameTest
{
    [Test]
    public async Task SelfUseCannotBypassUseDelay()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            maps.SetPaused(map, false);
            var coordinates = new MapCoordinates(Vector2.Zero, mapId);
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", coordinates);
            var gun = SEntMan.SpawnEntity("CMUYautjaHealingGun", coordinates);
            SEntMan.GetComponent<YautjaRecallableComponent>(gun).YautjaOwner = hunter;
            var damage = SEntMan.System<DamageableSystem>();
            var useDelay = SEntMan.GetComponent<UseDelayComponent>(gun);
            var useDelaySystem = SEntMan.System<UseDelaySystem>();

            Assert.That(damage.TryChangeDamage(hunter, BluntDamage(10), true), Is.Not.Null);
            var first = new UseInHandEvent(hunter);
            SEntMan.EventBus.RaiseLocalEvent(gun, first);
            Assert.That(first.Handled, Is.True);
            Assert.That(useDelaySystem.IsDelayed((gun, useDelay)), Is.True);

            Assert.That(damage.TryChangeDamage(hunter, BluntDamage(10), true), Is.Not.Null);
            var second = new UseInHandEvent(hunter);
            SEntMan.EventBus.RaiseLocalEvent(gun, second);
            Assert.That(second.Handled, Is.False);

            SEntMan.DeleteEntity(gun);
            SEntMan.DeleteEntity(hunter);
            SEntMan.DeleteEntity(map);
        });
    }

    [Test]
    public async Task InternalBleedingCanBeSelfRepaired()
    {
        EntityUid hunter = default;
        EntityUid gun = default;
        EntityUid torso = default;
        EntityUid map = default;

        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId, runMapInit: true);
            maps.SetPaused(map, false);
            var coordinates = new MapCoordinates(Vector2.Zero, mapId);
            hunter = SEntMan.SpawnEntity("CMUMobYautja", coordinates);
            gun = SEntMan.SpawnEntity("CMUYautjaHealingGun", coordinates);
            SEntMan.GetComponent<YautjaRecallableComponent>(gun).YautjaOwner = hunter;
            SEntMan.GetComponent<YautjaHealingGunComponent>(gun).DeepRepairDuration = TimeSpan.FromSeconds(0.1);
            Assert.That(SEntMan.System<SharedHandsSystem>()
                .TryPickupAnyHand(hunter, gun, checkActionBlocker: false), Is.True);

            var index = SEntMan.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetBodyPart(hunter,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None),
                out torso), Is.True);
            SEntMan.System<SharedCMUWoundsSystem>().SeedInternalBleed(torso, "yautja-test", 0.5f);

            var use = new UseInHandEvent(hunter);
            SEntMan.EventBus.RaiseLocalEvent(gun, use);
            Assert.Multiple(() =>
            {
                Assert.That(use.Handled, Is.True);
                Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(torso), Is.True);
            });
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(0.7f));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(torso), Is.False);
            SEntMan.DeleteEntity(hunter);
            SEntMan.DeleteEntity(map);
        });
    }

    private static DamageSpecifier BluntDamage(int amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", FixedPoint2.New(amount));
        return damage;
    }
}
