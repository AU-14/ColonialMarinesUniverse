using Content.IntegrationTests.Fixtures;
using Content.Shared.ActionBlocker;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUCaptiveToolsTest : GameTest
{
    [Test]
    public async Task CattleProdRequiresPowerAndToggleToStunXenos()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var prod = SEntMan.SpawnEntity("CMUHunterShipPlacedBaseItemStunprodSouthOffset0xNeg9", map.GridCoords);
            var batteries = Server.System<SharedBatterySystem>();
            var initial = batteries.GetCharge(prod);
            void Hit() => SEntMan.EventBus.RaiseLocalEvent(prod,
                new MeleeHitEvent([xeno], hunter, prod, new DamageSpecifier(), null));

            Hit();
            Assert.That(Server.System<ActionBlockerSystem>().CanMove(xeno), Is.True);
            Assert.That(batteries.GetCharge(prod), Is.EqualTo(initial));
            Assert.That(Server.System<ItemToggleSystem>().TryActivate(prod, hunter), Is.True);
            Hit();
            Assert.That(Server.System<ActionBlockerSystem>().CanMove(xeno), Is.False,
                "a powered cattle prod must incapacitate a xeno through the stun system");
            Assert.That(batteries.GetCharge(prod), Is.EqualTo(initial - 120), "one strike consumes one charge");

            var secondXeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            batteries.SetCharge(prod, 0);
            SEntMan.EventBus.RaiseLocalEvent(prod, new MeleeHitEvent([secondXeno], hunter, prod, new DamageSpecifier(), null));
            Assert.That(Server.System<ActionBlockerSystem>().CanMove(secondXeno), Is.True);
        });
    }

    [Test]
    public async Task XenoRestraintsBlockAttacksAndReleaseThroughNormalCuffFlow()
    {
        var map = await Pair.CreateTestMap();
        EntityUid hunter = default, xeno = default, restraints = default;
        var previous = ServerSession!.AttachedEntity;
        NetEntity xenoNet = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
                xenoNet = SEntMan.GetNetEntity(xeno);
                Server.PlayerMan.SetAttachedEntity(ServerSession, xeno);
                restraints = SEntMan.SpawnEntity("CMUHunterShipPlacedZiptiesLegcuffSouthOffset0x4", map.GridCoords);
                var cuffs = Server.System<SharedCuffableSystem>();
                Assert.That(cuffs.TryAddCuffsInstant(xeno, "Zipties"), Is.Null,
                    "ordinary human restraints must not become capable of binding xenos");
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(hunter, restraints), Is.True);
                var use = new AfterInteractEvent(hunter, restraints, xeno, map.GridCoords, true);
                SEntMan.EventBus.RaiseLocalEvent(restraints, use);
                Assert.That(use.Handled, Is.True);
            });
            await Pair.RunSeconds(5);
            await Client.WaitAssertion(() =>
                Assert.That(Client.System<ActionBlockerSystem>().CanAttack(CEntMan.GetEntity(xenoNet)), Is.False,
                    "the captive client must predict the same blocked attack state"));
            await Server.WaitAssertion(() =>
            {
                var cuffs = Server.System<SharedCuffableSystem>();
                Assert.That(cuffs.IsCuffed(xeno), Is.True);
                Assert.That(Server.System<ActionBlockerSystem>().CanAttack(xeno), Is.False);
                cuffs.Uncuff(xeno, hunter, restraints);
                Assert.That(cuffs.IsCuffed(xeno), Is.False);
                Assert.That(Server.System<ActionBlockerSystem>().CanAttack(xeno), Is.True);
            });
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
                Assert.That(Client.System<ActionBlockerSystem>().CanAttack(CEntMan.GetEntity(xenoNet)), Is.True));
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }
}
