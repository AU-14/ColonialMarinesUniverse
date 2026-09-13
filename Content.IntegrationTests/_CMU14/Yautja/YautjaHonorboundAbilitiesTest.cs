using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor.Magnetic;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee.Events;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHonorboundAbilitiesTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task CombatActionsAreGrantedOnlyToHonorboundYautja()
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            var actions = entities.System<SharedRMCActionsSystem>();
            var honorbound = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var badBlood = entities.SpawnEntity("CMUMobYautjaBadBloodGrunt", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(actions.GetActionsWithEvent<YautjaHonorRoarActionEvent>(honorbound).Count(), Is.EqualTo(1));
                Assert.That(actions.GetActionsWithEvent<YautjaHuntingLeapActionEvent>(honorbound).Count(), Is.EqualTo(1));
                Assert.That(actions.GetActionsWithEvent<YautjaHonorRoarActionEvent>(badBlood), Is.Empty);
                Assert.That(actions.GetActionsWithEvent<YautjaHuntingLeapActionEvent>(badBlood), Is.Empty);
            });

            var leapAction = actions.GetActionsWithEvent<YautjaHuntingLeapActionEvent>(honorbound).Single();
            Assert.That(entities.GetComponent<TargetActionComponent>(leapAction).Range, Is.EqualTo(7));
        });

        await Pair.Server.WaitPost(() => Pair.Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task HonorRoarAffectsEnemiesWithinFiveTilesOnly()
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            var actions = entities.System<SharedRMCActionsSystem>();
            var hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var nearEnemy = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var farEnemy = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(6, 0)));
            var ally = entities.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = actions.GetActionsWithEvent<YautjaHonorRoarActionEvent>(hunter).Single();
            var roar = new YautjaHonorRoarActionEvent { Performer = hunter, Action = action };

            entities.EventBus.RaiseLocalEvent(hunter, roar);

            Assert.Multiple(() =>
            {
                Assert.That(roar.Handled, Is.True);
                Assert.That(entities.HasComponent<ScreechBlindComponent>(nearEnemy), Is.True);
                Assert.That(entities.HasComponent<ScreechBlindComponent>(farEnemy), Is.False);
                Assert.That(entities.HasComponent<ScreechBlindComponent>(ally), Is.False);
            });
        });

        await Pair.Server.WaitPost(() => Pair.Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task HuntingLeapDecloaksStrikesAndBreaksSlingRecall()
    {
        var map = await Pair.CreateTestMap();
        var hunter = EntityUid.Invalid;
        var target = EntityUid.Invalid;
        var gun = EntityUid.Invalid;
        var originalAttached = Pair.Server.PlayerMan.Sessions.Single().AttachedEntity;

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            var attachments = entities.System<AttachableHolderSystem>();
            var actions = entities.System<SharedRMCActionsSystem>();
            hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            target = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            var sword = entities.SpawnEntity("CMUYautjaClanSword", map.GridCoords);
            gun = entities.SpawnEntity("WeaponShotgunM42A1", map.GridCoords);
            var sling = entities.SpawnEntity("RMCAttachmentTwoPointSling", map.GridCoords);

            Assert.That(hands.TryPickupAnyHand(hunter, sword, checkActionBlocker: false), Is.True);
            Assert.That(attachments.Attach((gun, entities.GetComponent<AttachableHolderComponent>(gun)), sling, target), Is.True);
            Assert.That(hands.TryPickupAnyHand(target, gun, checkActionBlocker: false), Is.True);
            Assert.That(entities.HasComponent<RMCMagneticItemComponent>(gun), Is.True);

            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Player!, hunter);
        });
        await Pair.RunUntilSynced();

        await Pair.Client.WaitAssertion(() =>
        {
            var probe = Pair.Client.System<YautjaHonorboundAbilityProbeSystem>();
            probe.Lunges = 0;
            probe.LastAnimation = string.Empty;
        });

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            var actions = entities.System<SharedRMCActionsSystem>();

            ToggleCloak(entities, hunter);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
            var action = actions.GetActionsWithEvent<YautjaHuntingLeapActionEvent>(hunter).Single();
            var leap = new YautjaHuntingLeapActionEvent
            {
                Performer = hunter,
                Action = action,
                Target = target,
            };
            entities.EventBus.RaiseLocalEvent(hunter, leap);
            Assert.That(leap.Handled, Is.True);
        });

        await Pair.RunTicksSync(10);

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False);
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(target).Int(), Is.GreaterThan(0));
                Assert.That(hands.IsHolding(target, gun), Is.False);
                Assert.That(entities.HasComponent<RMCMagneticItemComponent>(gun), Is.False);
            });

            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Player!, originalAttached);
        });

        await Pair.RunUntilSynced();
        await Pair.Client.WaitAssertion(() =>
        {
            var probe = Pair.Client.System<YautjaHonorboundAbilityProbeSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(probe.Lunges,
                    Is.EqualTo(1),
                    "A server-driven leap strike must send its normal melee animation to the attacking player.");
                Assert.That(probe.LastAnimation,
                    Is.EqualTo("WeaponArcThrust"),
                    "Leap must use the weapon's left-click animation, not its wide-attack animation.");
            });
        });

        await Pair.Server.WaitPost(() => Pair.Server.System<SharedMapSystem>().DeleteMap(map.MapId));
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

public sealed class YautjaHonorboundAbilityProbeSystem : EntitySystem
{
    public int Lunges;
    public string LastAnimation = string.Empty;

    public override void Initialize()
    {
        SubscribeNetworkEvent<MeleeLungeEvent>(ev =>
        {
            Lunges++;
            LastAnimation = ev.Animation ?? string.Empty;
        });
    }
}
