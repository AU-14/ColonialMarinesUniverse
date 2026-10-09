using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.NPC.Systems;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture, NonParallelizable]
public sealed partial class CMUExpeditionAdvancedAgentTest : GameTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> GOVFORPrototype = "GOVFOR";

    public override PoolSettings PoolSettings => new() { Dirty = true };

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

    [TestCase("CMUExpeditionScavengerAggressive", true)]
    [TestCase("CMUExpeditionScavengerCautious", false)]
    public async Task DispositionChangesExposureAndPressureChangesTheDecision(string prototype, bool presses)
    {
        EntityUid map = default, guard = default;
        await Server.WaitAssertion(() => (map, guard, _) = Arena(prototype));
        var heldAngle = false;
        var withdrew = false;
        var initiative = 0f;
        var trace = new List<string>();
        for (var sample = 0; sample < 60; sample++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                heldAngle |= agent.State == CMUExpeditionAgentState.HoldAngle;
                withdrew |= agent.State == CMUExpeditionAgentState.Withdraw;
                initiative = agent.Initiative;
                trace.Add($"{sample}: {agent.State}, pos={SEntMan.GetComponent<TransformComponent>(guard).Coordinates}, anchor={agent.CoverAnchor}, peek={agent.PeekPosition}, dest={agent.CoverDestination}, route={agent.Route.Count}, shots={agent.ShotsFired}, initiative={agent.Initiative}");
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(heldAngle, Is.EqualTo(presses), $"A confident raider follows up from the angle; a cautious sentry returns after the short volley.\n{string.Join(Environment.NewLine, trace)}");
            Assert.That(withdrew, Is.True, "Even aggressive soldiers must eventually return to cover.");
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
        });
        var pressedUnderFire = false;
        for (var sample = 0; sample < 20; sample++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() => pressedUnderFire |=
                SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State == CMUExpeditionAgentState.HoldAngle);
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(pressedUnderFire, Is.False, "A shaken soldier must stop extending exposure for another volley.");
            SEntMan.DeleteEntity(map);
        });
    }

    private (EntityUid Map, EntityUid Guard, EntityUid Enemy) Arena(string prototype)
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
        for (var y = 1; y <= 4; y++)
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
