using Content.Server.Humanoid;
using Content.Shared.Damage.Systems;
using Content.Shared.Blocking.Components;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Content.Client.Popups;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Tether;
using Content.Shared.Actions.Components;
using Content.Shared.Blocking;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMeleeWeaponTest
{

    [Test]
    public async Task ChainedWeaponPickupAndDeletionCleanCmss13TetherAndRecallAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var owner = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var nonOwner = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var pickedUp = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var deleted = entMan.SpawnEntity("CMUYautjaWarAxe", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(owner);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(nonOwner);
                SetupChainedLink(entMan, owner, pickedUp);
                SetupChainedLink(entMan, owner, deleted);

                var pickupAttempt = new GettingPickedUpAttemptEvent(nonOwner, pickedUp, true);
                entMan.EventBus.RaiseLocalEvent(pickedUp, pickupAttempt);
                Assert.That(pickupAttempt.Cancelled, Is.False,
                    "CMSS13 on_pickup() warns the original holder and cleans up the chain, but does not block pickup.");

                var handEvent = new GotEquippedHandEvent(nonOwner, pickedUp, default!);
                entMan.EventBus.RaiseLocalEvent(pickedUp, handEvent);

                entMan.DeleteEntity(deleted);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<YautjaChainedWeaponComponent>(pickedUp).LinkedTo, Is.Null,
                        "CMSS13 on_pickup() calls cleanup_chain() after a non-owner pickup.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(pickedUp), Is.False,
                        "CMSS13 cleanup_chain() deletes the active tether.");
                    Assert.That(entMan.Deleted(deleted), Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { owner, nonOwner, pickedUp, deleted })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainedWeaponEnteringContainerRecallsAndCleansChainLikeCmss13OnMove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid containerOwner = default;
        EntityUid combistick = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var containers = entMan.System<SharedContainerSystem>();
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                containerOwner = entMan.SpawnEntity("CMCrowbar", map.GridCoords.Offset(new(1, 0)));
                combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new(3, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                EquipBracer(entMan, hunter, bracer);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;
                SetupChainedLink(entMan, hunter, combistick);

                var container = containers.EnsureContainer<Container>(containerOwner, "cmu-yautja-chained-test");
                Assert.That(containers.Insert(combistick, container, force: true), Is.True);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var containers = entMan.System<SharedContainerSystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var chained = entMan.GetComponent<YautjaChainedWeaponComponent>(combistick);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var hunterCoords = transform.GetMapCoordinates(hunter);
                var combistickCoords = transform.GetMapCoordinates(combistick);

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, combistick), Is.True,
                        "CMSS13 on_move() recalls a chained weapon that enters container-like storage.");
                    Assert.That(containers.TryGetContainingContainer(combistick, out var containing), Is.True,
                        "The recalled chained weapon should end up in the hunter's hand container.");
                    Assert.That(containing!.Owner, Is.Not.EqualTo(containerOwner),
                        "The recalled chained weapon must leave the inserted container.");
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 230),
                        "CMSS13 recall() drains 70 power after pulling the chained weapon back.");
                    Assert.That(chained.LinkedTo, Is.Null,
                        "CMSS13 recall() calls cleanup_chain() after successful drain.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(combistick), Is.False,
                        "CMSS13 cleanup_chain() deletes/nulls the tether after successful recall.");
                    Assert.That(combistickCoords.MapId, Is.EqualTo(hunterCoords.MapId));
                    Assert.That(combistickCoords.Position, Is.EqualTo(hunterCoords.Position));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, bracer, containerOwner, combistick })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainedWeaponBloodChargeGatesThrowLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var warAxe = entMan.SpawnEntity("CMUYautjaWarAxe", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var combi = entMan.GetComponent<YautjaChainedWeaponComponent>(combistick);
                var axe = entMan.GetComponent<YautjaChainedWeaponComponent>(warAxe);

                var refused = RaiseThrowItemAttempt(entMan, combistick, hunter);
                RaiseMeleeHit(entMan, combistick, hunter, hunter);
                var selfHitThrow = RaiseThrowItemAttempt(entMan, combistick, hunter);

                RaiseMeleeHit(entMan, combistick, hunter, target);
                var charged = combi.Charged;
                var allowed = RaiseThrowItemAttempt(entMan, combistick, hunter);

                axe.Charged = true;
                var axeAllowed = RaiseThrowItemAttempt(entMan, warAxe, hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(refused.Cancelled, Is.True,
                        "CMSS13 try_to_throw() refuses an uncharged chained weapon.");
                    Assert.That(selfHitThrow.Cancelled, Is.True,
                        "CMSS13 chained attack() returns before charging when target == user.");
                    Assert.That(charged, Is.True,
                        "CMSS13 chained attack() charges the reservoir on first valid living prey hit.");
                    Assert.That(allowed.Cancelled, Is.False,
                        "CMSS13 try_to_throw() allows a charged chained weapon to leave the hand.");
                    Assert.That(combi.Charged, Is.False,
                        "CMSS13 try_to_throw() consumes the blood charge before the throw.");
                    Assert.That(axeAllowed.Cancelled, Is.False,
                        "CMSS13 /obj/item/weapon/yautja/chained/war_axe inherits the chained throw gate.");
                    Assert.That(axe.Charged, Is.False,
                        "War axe charged throws also consume the inherited chained charge.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, target, combistick, warAxe })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaCatchesThrownChainedWeaponWithoutThrowDamageLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<StaminaComponent>(hunter);
                var damageable = entMan.GetComponent<DamageableComponent>(hunter);
                var stamina = entMan.GetComponent<StaminaComponent>(hunter);
                var thrown = entMan.EnsureComponent<ThrownItemComponent>(combistick);
                thrown.Thrower = hunter;

                var hit = new ThrowDoHitEvent(combistick, hunter, thrown);
                entMan.EventBus.RaiseLocalEvent(combistick, ref hit);

                Assert.Multiple(() =>
                {
                    Assert.That(hit.Handled, Is.True,
                        "CMSS13 chained weapon launch_impact() returns early for Yautja catches.");
                    Assert.That(hands.IsHolding(hunter, combistick), Is.True,
                        "CMSS13 chained weapon launch_impact() makes a Yautja put the chained weapon in hand.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(combistick), Is.False,
                        "A successful local catch must stop the thrown item so it is no longer flying.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, damageable)).GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                        "CMSS13 chained weapon catch returns before parent launch_impact(), so throwforce damage is not applied to the Yautja catcher.");
                    Assert.That(stamina.StaminaDamage, Is.EqualTo(0f),
                        "The local handled Yautja catch must skip generic throw-hit stamina.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, combistick })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullHandedYautjaChainedWeaponHitFallsThroughToThrowDamageLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var firstItem = entMan.SpawnEntity("CMCrowbar", map.GridCoords);
            var secondItem = entMan.SpawnEntity("CMCrowbar", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<StaminaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, firstItem), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, secondItem), Is.True);

                var damageable = entMan.GetComponent<DamageableComponent>(hunter);
                var stamina = entMan.GetComponent<StaminaComponent>(hunter);
                var thrown = entMan.EnsureComponent<ThrownItemComponent>(combistick);
                thrown.Thrower = hunter;

                var hit = new ThrowDoHitEvent(combistick, hunter, thrown);
                entMan.EventBus.RaiseLocalEvent(combistick, ref hit);

                Assert.Multiple(() =>
                {
                    Assert.That(hit.Handled, Is.False,
                        "CMSS13 chained weapon launch_impact() returns early only when human.put_in_hands(src) succeeds; full hands should fall through to parent impact.");
                    Assert.That(hands.IsHolding(hunter, combistick), Is.False,
                        "A full-handed Yautja cannot catch the chained weapon because CMSS13 put_in_hands(src) failed.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(combistick), Is.True,
                        "The local thrown item should not be stopped by the failed catch branch.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, damageable)).GetTotal(), Is.GreaterThan(FixedPoint2.Zero),
                        "Falling through to parent launch_impact() should allow local generic throw damage.");
                    Assert.That(stamina.StaminaDamage, Is.EqualTo(0f),
                        "Combistick only carries local throw damage, not a stamina-on-collide component.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, firstItem, secondItem, combistick })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullHandedCallCombiDropsWeaponAtHunterFeetLikeCmss13Recall()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var firstItem = entMan.SpawnEntity("CMCrowbar", map.GridCoords);
            var secondItem = entMan.SpawnEntity("CMCrowbar", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new(6, 0)));
            var action = entMan.SpawnEntity("CMUActionYautjaCallCombi", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                EquipBracer(entMan, hunter, bracer);
                Assert.That(hands.TryPickupAnyHand(hunter, firstItem), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, secondItem), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;
                var chained = entMan.GetComponent<YautjaChainedWeaponComponent>(combistick);
                chained.LinkedTo = hunter;

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var call = new YautjaCallCombiActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(combistick, call);

                var hunterCoords = transform.GetMapCoordinates(hunter);
                var combistickCoords = transform.GetMapCoordinates(combistick);
                Assert.Multiple(() =>
                {
                    Assert.That(call.Handled, Is.True);
                    Assert.That(hands.IsHolding(hunter, combistick), Is.False,
                        "CMSS13 recall() drops the chained weapon at the hunter's feet when put_in_hands(src, TRUE) fails.");
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 230));
                    Assert.That(chained.LinkedTo, Is.Null,
                        "CMSS13 recall() calls cleanup_chain() after the full-hands drop branch drains power.");
                    Assert.That(combistickCoords.MapId, Is.EqualTo(hunterCoords.MapId));
                    Assert.That(combistickCoords.Position, Is.EqualTo(hunterCoords.Position));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, bracer, firstItem, secondItem, combistick, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static MeleeHitEvent RaiseMeleeHit(IEntityManager entMan, EntityUid weapon, EntityUid user, EntityUid target)
    {
        return RaiseMeleeHit(entMan, weapon, user, target, new DamageSpecifier());
    }

    private static MeleeHitEvent RaiseMeleeHit(
        IEntityManager entMan,
        EntityUid weapon,
        EntityUid user,
        EntityUid target,
        DamageSpecifier baseDamage,
        bool isHit = true)
    {
        var hit = new MeleeHitEvent(new List<EntityUid> { target }, user, weapon, baseDamage, null)
        {
            IsHit = isHit,
        };
        entMan.EventBus.RaiseLocalEvent(weapon, hit);
        return hit;
    }

    // a plain left click on the corpse with the dagger in the active hand

    private static void EquipBracer(IEntityManager entMan, EntityUid user, EntityUid bracer)
    {
        var inventory = entMan.System<InventorySystem>();
        Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
    }

    private static void SetupChainedLink(IEntityManager entMan, EntityUid owner, EntityUid weapon)
    {
        var chained = entMan.GetComponent<YautjaChainedWeaponComponent>(weapon);
        chained.LinkedTo = owner;
        chained.TetherRange = 6f;
        var tether = entMan.EnsureComponent<RMCTetherComponent>(weapon);
        tether.TetherOrigin = owner;
        tether.StaticTetherOrigin = entMan.System<SharedTransformSystem>().GetMapCoordinates(owner);
    }

    private static ThrowItemAttemptEvent RaiseThrowItemAttempt(IEntityManager entMan, EntityUid item, EntityUid user)
    {
        var attempt = new ThrowItemAttemptEvent(user);
        entMan.EventBus.RaiseLocalEvent(item, ref attempt);
        return attempt;
    }

    // CMU14: Yautja melee weapons must not perform wide (arc) attacks. The restriction lives on the shared
    // MeleeWeaponComponent.WideAttackAllowed plus a guard in SharedMeleeWeaponSystem.AttemptAttack (server) and
    // MeleeWeaponSystem.ClientHeavyAttack (client), and every CMUYautjaWeaponBase child opts out here.

}
