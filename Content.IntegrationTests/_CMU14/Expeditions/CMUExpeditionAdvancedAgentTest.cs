using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture, NonParallelizable]
public sealed partial class CMUExpeditionAdvancedAgentTest : GameTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> GOVFORPrototype = "GOVFOR";

    // fixed seed, these assert emergent fights and a fresh seed per pair made a different one miss each run
    public override PoolSettings PoolSettings => new() { Dirty = true, ServerSeed = 1414 };

    // learned tactics carry over between rounds on a recycled server, start every case from the baseline
    [SetUp]
    public async Task ResetExpeditionLearning() =>
        await Server.WaitPost(() => Server.System<CMUExpeditionAgentSystem>().ResetLearnedExperience());

    [Test]
    public async Task ModerateWoundsWaitForALullWhileTheGuardKeepsFighting()
    {
        EntityUid map = default, guard = default, enemy = default, medicine = default, rifle = default;
        var initialAmmo = 0;
        await Server.WaitAssertion(() =>
        {
            (map, guard, enemy) = Arena("CMUExpeditionScavenger");
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(guard, "pocket1", out var item), Is.True);
            medicine = item!.Value;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            initialAmmo = Ammo(rifle);
            Hurt(guard, 30);
        });
        for (var i = 0; i < 40; i++)
        {
            await Pair.RunSeconds(0.2f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State,
                    Is.Not.EqualTo(CMUExpeditionAgentState.Healing), "A moderate wound must not cancel every attack to apply a dressing.");
                Assert.That(SEntMan.GetComponent<StackComponent>(medicine).Count, Is.EqualTo(3));
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(rifle), Is.LessThan(initialAmmo), "The patient must still contribute to the fight while postponing treatment.");
            SEntMan.DeleteEntity(enemy);
        });
        await Pair.RunSeconds(12);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(guard).Float(), Is.LessThan(30),
                "Once contact expires, use the lull for real medical treatment.");
            Assert.That(SEntMan.GetComponent<StackComponent>(medicine).Count, Is.LessThan(3));
            SEntMan.DeleteEntity(map);
        });
    }

    [TestCase("CMUExpeditionScavengerAggressive")]
    [TestCase("CMUExpeditionScavengerCautious")]
    public async Task InfantryHoldsProductiveAnglesButWithdrawsWhenBadlyWounded(string prototype)
    {
        EntityUid map = default, guard = default, enemy = default, rifle = default;
        EntityCoordinates initial = default;
        var initialAmmo = 0;
        await Server.WaitAssertion(() =>
        {
            (map, guard, enemy) = Arena(prototype);
            initial = SEntMan.GetComponent<TransformComponent>(guard).Coordinates;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            initialAmmo = Ammo(rifle);
        });
        var heldAngle = false;
        var initiative = 0f;
        for (var sample = 0; sample < 60; sample++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                heldAngle |= agent.State == CMUExpeditionAgentState.HoldAngle;
                initiative = agent.Initiative;
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(heldAngle, Is.True);
            Assert.That(Ammo(rifle), Is.LessThan(initialAmmo), "A productive stance must deliver real fire.");
            Assert.That(Server.System<SharedTransformSystem>().InRange(initial,
                SEntMan.GetComponent<TransformComponent>(guard).Coordinates, 0.5f), Is.True,
                "An unopposed rifleman should keep a clear firing position across volleys.");
        });
        for (var hit = 0; hit < 3; hit++)
        {
            await Server.WaitAssertion(() => Hurt(guard, 10));
            await Pair.RunSeconds(0.2f);
        }
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.Emotion, Is.EqualTo(CMUExpeditionEmotion.Shaken));
            Assert.That(agent.Initiative, Is.LessThan(initiative), "Fresh pressure must change decisions rather than just the displayed mood.");
            Hurt(guard, (int) agent.EmergencyHealDamage - 30);
        });
        var sheltered = false;
        for (var sample = 0; sample < 100 && !sheltered; sample++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() => sheltered = !Server.System<SharedInteractionSystem>().InRangeUnobstructed(
                guard, enemy, 100, CollisionGroup.Impassable | CollisionGroup.InteractImpassable,
                predicate: entity => entity == guard || entity == enemy));
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(sheltered, Is.True, "Severe wounds must override position commitment and physically break enemy sight.");
            SEntMan.DeleteEntity(map);
        });
    }

    private (EntityUid Map, EntityUid Guard, EntityUid Enemy) Arena(string prototype, bool withCover = true)
    {
        var generator = Server.System<CMUExpeditionSystem>();
        Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
            CMUExpeditionStory.CrashRecovery, out var map, out var error), Is.True, error);
        var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
        for (var i = 0; i < 900 && !expedition.Ready; i++)
            generator.Update(0);
        Assert.That(expedition.Ready, Is.True);
        var lz = expedition.Plan.LandingZone;
        var origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));
        var guard = SEntMan.SpawnEntity(prototype, origin.Offset(new Vector2(-4, 0)));
        var enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(5, 0)));
        SEntMan.AddComponent<GodmodeComponent>(enemy);
        Server.System<NpcFactionSystem>().AddFaction(enemy, GOVFORPrototype);
        for (var y = 1; withCover && y <= 4; y++)
        {
            var coordinates = origin.Offset(new Vector2(-2, y));
            SEntMan.SpawnEntity("CMUExpeditionHull", coordinates);
            expedition.Plan.Props[expedition.Plan.Index((int) coordinates.X, (int) coordinates.Y)] = CMUExpeditionProp.Hull;
        }
        return (map, guard, enemy);
    }

    private int Ammo(EntityUid gun)
    {
        var count = new GetAmmoCountEvent();
        SEntMan.EventBus.RaiseLocalEvent(gun, ref count);
        return count.Count;
    }

    private void Hurt(EntityUid guard, int amount) => Server.System<DamageableSystem>().TryChangeDamage(guard,
        new DamageSpecifier { DamageDict = { ["Blunt"] = amount } }, ignoreResistances: true);
}
