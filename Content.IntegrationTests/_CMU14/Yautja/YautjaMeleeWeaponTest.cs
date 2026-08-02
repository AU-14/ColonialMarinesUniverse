using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Content.Client.Popups;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Yautja;
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

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMeleeWeaponTest
{
    [Test]
    public async Task YautjaMeleeWeaponsApplyCmss13XenoInterferenceDurations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            var chainwhip = entMan.SpawnEntity("CMUYautjaChainwhip", map.GridCoords);
            var sword = entMan.SpawnEntity("CMUYautjaClanSword", map.GridCoords);
            var scythe = entMan.SpawnEntity("CMUYautjaDualWarScythe", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var glaive = entMan.SpawnEntity("CMUYautjaWarGlaive", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                RaiseMeleeHit(entMan, chainwhip, hunter, human);
                Assert.That(status.TryGetTime(human, "YautjaInterference", out _), Is.False,
                    "CMSS13 Yautja melee interference only applies when isxeno(target).");

                AssertWeaponAppliesInterference(entMan, status, chainwhip, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/yautja/chain/attack() adds interference(30, 30) to xenos.");

                AssertWeaponAppliesInterference(entMan, status, sword, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/yautja/sword/attack() adds interference(30, 30) to xenos.");

                AssertWeaponAppliesInterference(entMan, status, scythe, hunter, TimeSpan.FromSeconds(15),
                    "CMSS13 /obj/item/weapon/yautja/scythe/attack() adds interference(15, 15) to xenos.");

                AssertWeaponAppliesInterference(entMan, status, combistick, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/yautja/chained/attack() adds interference(30, 30) to xenos.");

                AssertWeaponAppliesInterference(entMan, status, glaive, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/twohanded/yautja/glaive/attack() adds interference(30, 30) to xenos.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, human, chainwhip, sword, scythe, combistick, glaive })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMeleeXenoInterferenceRequiresYautjaSpeciesLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var techHuman = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var chainwhip = entMan.SpawnEntity("CMUYautjaChainwhip", map.GridCoords);
            var sword = entMan.SpawnEntity("CMUYautjaClanSword", map.GridCoords);
            var scythe = entMan.SpawnEntity("CMUYautjaDualWarScythe", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var glaive = entMan.SpawnEntity("CMUYautjaWarGlaive", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techHuman);

                AssertWeaponDoesNotApplyInterference(entMan, status, chainwhip, techHuman,
                    "CMSS13 /obj/item/weapon/yautja/chain/attack() gates xeno interference on human_adapted || isyautja(user), not TRAIT_YAUTJA_TECH.");

                AssertWeaponDoesNotApplyInterference(entMan, status, sword, techHuman,
                    "CMSS13 /obj/item/weapon/yautja/sword/attack() gates xeno interference on human_adapted || isyautja(user), not TRAIT_YAUTJA_TECH.");

                AssertWeaponDoesNotApplyInterference(entMan, status, scythe, techHuman,
                    "CMSS13 /obj/item/weapon/yautja/scythe/attack() gates xeno interference on human_adapted || isyautja(user), not TRAIT_YAUTJA_TECH.");

                AssertWeaponDoesNotApplyInterference(entMan, status, combistick, techHuman,
                    "CMSS13 /obj/item/weapon/yautja/chained/attack() gates xeno interference on human_adapted || isspeciesyautja(user), not TRAIT_YAUTJA_TECH.");

                AssertWeaponDoesNotApplyInterference(entMan, status, glaive, techHuman,
                    "CMSS13 /obj/item/weapon/twohanded/yautja/glaive/attack() gates xeno interference on human_adapted || isyautja(user), not TRAIT_YAUTJA_TECH.");
            }
            finally
            {
                foreach (var uid in new[] { techHuman, chainwhip, sword, scythe, combistick, glaive })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WarAxeAndGlaiveVariantsInheritCmss13XenoInterferenceCallbacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var warAxe = entMan.SpawnEntity("CMUYautjaWarAxe", map.GridCoords);
            var cleaving = entMan.SpawnEntity("CMUYautjaCleavingGlaive", map.GridCoords);
            var ancient = entMan.SpawnEntity("CMUYautjaAncientWarGlaive", map.GridCoords);
            var longaxe = entMan.SpawnEntity("CMUYautjaLongaxe", map.GridCoords);

            try
            {
                AssertWeaponAppliesInterference(entMan, status, warAxe, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/yautja/chained/war_axe inherits /chained/attack() interference(30, 30).");

                AssertWeaponAppliesInterference(entMan, status, cleaving, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/twohanded/yautja/glaive/alt inherits /glaive/attack() interference(30, 30).");

                AssertWeaponAppliesInterference(entMan, status, ancient, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/twohanded/yautja/glaive/damaged inherits /glaive/attack() interference(30, 30).");

                AssertWeaponAppliesInterference(entMan, status, longaxe, hunter, TimeSpan.FromSeconds(30),
                    "CMSS13 /obj/item/weapon/twohanded/yautja/glaive/longaxe inherits /glaive/attack() interference(30, 30).");
            }
            finally
            {
                foreach (var uid in new[] { hunter, warAxe, cleaving, ancient, longaxe })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CleavingGlaiveMountsHumanSkullTrophyLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<Content.Server.Examine.ExamineSystem>();
            var containers = entMan.System<SharedContainerSystem>();
            var appearance = entMan.System<SharedAppearanceSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var glaive = entMan.SpawnEntity("CMUYautjaCleavingGlaive", map.GridCoords);
            var skull = entMan.SpawnEntity("CMUYautjaHumanSkullTrophy", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, glaive), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, skull), Is.True);

                var interact = new InteractUsingEvent(
                    hunter,
                    skull,
                    glaive,
                    entMan.GetComponent<TransformComponent>(glaive).Coordinates);

                entMan.EventBus.RaiseLocalEvent(glaive, interact);

                containers.TryGetContainer(glaive, "cmu-yautja-cleaving-glaive-skull", out var container);
                appearance.TryGetData<bool>(glaive, ToggleableVisuals.Enabled, out var skullAttached);
                var message = examine.GetExamineText(glaive, hunter).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True,
                        "CMSS13 /obj/item/weapon/twohanded/yautja/glaive/alt/attackby() handles a human skull.");
                    Assert.That(skullAttached, Is.True,
                        "CMSS13 cleaving glaive sets skull_attached = TRUE and update_icon() switches to the skull state.");
                    Assert.That(container, Is.Not.Null);
                    Assert.That(((ContainerSlot) container!).ContainedEntity, Is.EqualTo(skull),
                        "CMSS13 drop_inv_item_to_loc(skull, src) keeps the mounted skull inside the glaive.");
                    Assert.That(message, Does.Contain("has a human skull mounted on it"),
                        "CMSS13 cleaving glaive examine appends that mounted-skull notice.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, glaive, skull })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WarScythesUseCmss13FifteenPercentBonusStrike()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var dualScythe = entMan.SpawnEntity("CMUYautjaDualWarScythe", map.GridCoords);
            var doubleScythe = entMan.SpawnEntity("CMUYautjaDoubleWarScythe", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                AssertScytheBonusStrike(entMan, dualScythe, hunter, target, "CMSS13 /obj/item/weapon/yautja/scythe/attack()");
                AssertScytheBonusStrike(entMan, doubleScythe, hunter, target, "CMSS13 /obj/item/weapon/yautja/scythe/alt inherits /scythe/attack().");
            }
            finally
            {
                foreach (var uid in new[] { hunter, target, dualScythe, doubleScythe })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletExamineHasNoCombatIntentGuidance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<Content.Server.Examine.ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var techAuthorizedHuman = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techAuthorizedHuman);

                var hunterText = examine.GetExamineText(gauntlet, hunter).ToMarkup();
                var techHumanText = examine.GetExamineText(gauntlet, techAuthorizedHuman).ToMarkup();
                var humanText = examine.GetExamineText(gauntlet, human).ToMarkup();

                Assert.Multiple(() =>
                {
                    AssertNoCombatIntentGuidance(hunterText);
                    AssertNoCombatIntentGuidance(techHumanText);
                    AssertNoCombatIntentGuidance(humanText);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, techAuthorizedHuman, human, gauntlet })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertNoCombatIntentGuidance(string text)
    {
        var lower = text.ToLowerInvariant();
        Assert.That(lower, Does.Not.Contain("harm"));
        Assert.That(lower, Does.Not.Contain("help"));
        Assert.That(lower, Does.Not.Contain("shove"));
        Assert.That(lower, Does.Not.Contain("grab"));
        Assert.That(lower, Does.Not.Contain("stack up your combo meter"));
        Assert.That(lower, Does.Not.Contain("finish your combo"));
    }

    [Test]
    public async Task NonTechChainedWeaponPickupStartsCmss13UntangleDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.GetComponent<YautjaChainedWeaponComponent>(combistick).LinkedTo = hunter;

                var interact = new InteractHandEvent(human, combistick);
                entMan.EventBus.RaiseLocalEvent(combistick, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True,
                        "CMSS13 /obj/item/weapon/yautja/chained/attack_hand() handles non-tech pickup by starting untangle do_after before parent pickup.");
                    Assert.That(ActiveChainedUntangleDoAfters(entMan, human), Is.EqualTo(1));
                    Assert.That(ActiveChainedUntangleDelay(entMan, human), Is.EqualTo(TimeSpan.FromSeconds(3)),
                        "CMSS13 chained weapon attack_hand() uses do_after(user, 3 SECONDS, INTERRUPT_ALL, BUSY_ICON_HOSTILE, src, INTERRUPT_MOVED, BUSY_ICON_HOSTILE).");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, human, combistick })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaUseInHandWieldsExtendedCombistickLikeCmss13AttackSelf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var melee = entMan.System<SharedMeleeWeaponSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);

            try
            {
                Assert.That(hands.TryPickupAnyHand(human, combistick, checkActionBlocker: false), Is.True);
                Assert.That(hands.GetActiveItem(human), Is.EqualTo(combistick));

                var use = new UseInHandEvent(human);
                entMan.EventBus.RaiseLocalEvent(combistick, use);

                var toggle = entMan.GetComponent<ItemToggleComponent>(combistick);
                var item = entMan.GetComponent<ItemComponent>(combistick);
                var wieldable = entMan.GetComponent<WieldableComponent>(combistick);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True,
                        "CMSS13 /obj/item/weapon/yautja/chained/attack_self() handles a held extended combi-stick by wielding it.");
                    Assert.That(toggle.Activated, Is.True,
                        "CMSS13 attack_self() does not collapse the combi-stick; folding is the separate unique_action()/verb path.");
                    Assert.That(wieldable.Wielded, Is.True,
                        "CMSS13 attack_self() wields an extended combi-stick when it is not already wielded.");
                    Assert.That(item.Size.Id, Is.EqualTo("Large"),
                        "CMSS13 attack_self() keeps the extended combi-stick at w_class = SIZE_LARGE.");
                    Assert.That(DamageTotal(melee.GetDamage(combistick, human)), Is.EqualTo((FixedPoint2) 30),
                        "CMSS13 wield() sets force = force_wielded = MELEE_FORCE_TIER_6.");
                    Assert.That(entMan.HasComponent<SharpComponent>(combistick), Is.True,
                        "CMSS13 attack_self() keeps the extended sharp combi-stick state.");
                });
            }
            finally
            {
                foreach (var uid in new[] { human, combistick })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExtendedCombistickUsesCmss13UnwieldedAndWieldedForce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var melee = entMan.System<SharedMeleeWeaponSystem>();
            var wield = entMan.System<SharedWieldableSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);

            try
            {
                Assert.That(hands.TryPickupAnyHand(hunter, combistick, checkActionBlocker: false), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent<WieldableComponent>(combistick, out _), Is.True,
                        "CMSS13 combistick has flags_item = TWOHANDED while extended.");
                    Assert.That(entMan.TryGetComponent<IncreaseDamageOnWieldComponent>(combistick, out var bonus), Is.True,
                        "CMSS13 combistick force_wielded = MELEE_FORCE_TIER_6 must be represented as a local wield bonus over force_unwielded.");
                    if (bonus != null)
                    {
                        Assert.That(DamageTotal(bonus.BonusDamage), Is.EqualTo((FixedPoint2) 20),
                            "CMSS13 force_wielded 30 minus force_unwielded 10 maps to a 20 local wield bonus.");
                    }
                });

                var unwielded = melee.GetDamage(combistick, hunter);
                Assert.That(DamageTotal(unwielded), Is.EqualTo((FixedPoint2) 10),
                    "CMSS13 combistick force_unwielded = MELEE_FORCE_TIER_2 while extended but not wielded.");

                var wieldable = entMan.GetComponent<WieldableComponent>(combistick);
                Assert.That(wield.TryWield(combistick, wieldable, hunter), Is.True);

                var wielded = melee.GetDamage(combistick, hunter);
                Assert.That(DamageTotal(wielded), Is.EqualTo((FixedPoint2) 30),
                    "CMSS13 combistick force_wielded = MELEE_FORCE_TIER_6 while extended and wielded.");
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
    public async Task DroppedChainedWeaponCreatesCmss13NonResistibleTether()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);
                Assert.That(hands.TryDrop(hunter, combistick, checkActionBlocker: false), Is.True);

                var chained = entMan.GetComponent<YautjaChainedWeaponComponent>(combistick);
                var tether = entMan.GetComponent<RMCTetherComponent>(combistick);

                Assert.Multiple(() =>
                {
                    Assert.That(chained.LinkedTo, Is.EqualTo(hunter),
                        "CMSS13 /obj/item/weapon/yautja/chained/dropped() calls setup_chain(user).");
                    Assert.That(chained.TetherRange, Is.EqualTo(6f),
                        "CMSS13 setup_chain() applies a non-resistible tether with range = 6.");
                    Assert.That(tether.TetherOrigin, Is.EqualTo(hunter),
                        "CMSS13 setup_chain() links the tether to the hunter who dropped the chained weapon.");
                    Assert.That(tether.StaticTetherOrigin, Is.Not.Null,
                        "The local tether must keep a map-coordinate origin for the chained weapon visual.");
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

                var pickupAttempt = new GettingPickedUpAttemptEvent(nonOwner, pickedUp);
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
    public async Task ChainedWeaponAnimalHitDoesNotBloodChargeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new(1, 0)));
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var chained = entMan.GetComponent<YautjaChainedWeaponComponent>(combistick);

                RaiseMeleeHit(entMan, combistick, hunter, hellhound);
                var afterAnimalHit = chained.Charged;
                var animalHitThrow = RaiseThrowItemAttempt(entMan, combistick, hunter);

                RaiseMeleeHit(entMan, combistick, hunter, human);
                var afterHumanHit = chained.Charged;

                Assert.Multiple(() =>
                {
                    Assert.That(afterAnimalHit, Is.False,
                        "CMSS13 /obj/item/weapon/yautja/chained/attack() returns before charging when isanimal(target).");
                    Assert.That(animalHitThrow.Cancelled, Is.True,
                        "An animal-only hit must leave the chained weapon uncharged, so the inherited throw gate still refuses it.");
                    Assert.That(afterHumanHit, Is.True,
                        "The animal exclusion must not suppress the normal living prey blood-charge path.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, hellhound, human, combistick })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaShieldsRaiseAndLowerUseCmss13ReadyHeldVisuals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var blocking = entMan.System<BlockingSystem>();

            foreach (var (prototype, heldPrefix) in new[]
                     {
                         ("CMUYautjaClanShield", "clan_shield"),
                         ("CMUYautjaAncientShield", "ancient_shield"),
                         ("CMUYautjaAncientShieldAlt", "ancient_shield_alt"),
                         ("CMUYautjaAncientShieldTemple", "ancient_shield_temple"),
                     })
            {
                var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                var shield = entMan.SpawnEntity(prototype, map.GridCoords);

                try
                {
                    entMan.EnsureComponent<YautjaComponent>(hunter);
                    Assert.That(hands.TryPickupAnyHand(hunter, shield), Is.True, prototype);

                    var item = entMan.GetComponent<ItemComponent>(shield);
                    var block = entMan.GetComponent<BlockingComponent>(shield);

                    Assert.That(item.HeldPrefix, Is.EqualTo(heldPrefix), prototype);
                    Assert.That(blocking.StartBlocking(shield, block, hunter), Is.True, prototype);
                    Assert.That(item.HeldPrefix, Is.EqualTo($"{heldPrefix}_ready"),
                        $"CMSS13 /obj/item/weapon/shield/riot/yautja/raise_shield() sets item_state to [base_icon_state]_ready for {prototype}.");

                    Assert.That(blocking.StopBlocking(shield, block, hunter), Is.True, prototype);
                    Assert.That(item.HeldPrefix, Is.EqualTo(heldPrefix),
                        $"CMSS13 /obj/item/weapon/shield/riot/yautja/lower_shield() restores item_state to base_icon_state for {prototype}.");
                }
                finally
                {
                    foreach (var uid in new[] { hunter, shield })
                    {
                        if (!entMan.Deleted(uid))
                            entMan.DeleteEntity(uid);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMeleeWeaponsExposeCmss13PounceBlockFlags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            const string pounceBlock = "YautjaPounceBlock";

            foreach (var prototype in new[]
                     {
                         "CMUYautjaDualWarScythe",
                         "CMUYautjaDoubleWarScythe",
                         "CMUYautjaCruelStaff",
                         "CMUYautjaCombistick",
                         "CMUYautjaWarGlaive",
                         "CMUYautjaCleavingGlaive",
                         "CMUYautjaAncientWarGlaive",
                         "CMUYautjaLongaxe",
                     })
            {
                var entityPrototype = prototypes.Index<EntityPrototype>(prototype);
                Assert.That(entityPrototype.Components, Does.ContainKey(pounceBlock),
                    $"{prototype} should preserve CMSS13 shield_flags = CAN_BLOCK_POUNCE from yaut_weapons.dm.");
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
                entMan.EventBus.RaiseLocalEvent(combistick, hit);

                Assert.Multiple(() =>
                {
                    Assert.That(hit.Handled, Is.True,
                        "CMSS13 chained weapon launch_impact() returns early for Yautja catches.");
                    Assert.That(hands.IsHolding(hunter, combistick), Is.True,
                        "CMSS13 chained weapon launch_impact() makes a Yautja put the chained weapon in hand.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(combistick), Is.False,
                        "A successful local catch must stop the thrown item so it is no longer flying.");
                    Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero),
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
                entMan.EventBus.RaiseLocalEvent(combistick, hit);

                Assert.Multiple(() =>
                {
                    Assert.That(hit.Handled, Is.False,
                        "CMSS13 chained weapon launch_impact() returns early only when human.put_in_hands(src) succeeds; full hands should fall through to parent impact.");
                    Assert.That(hands.IsHolding(hunter, combistick), Is.False,
                        "A full-handed Yautja cannot catch the chained weapon because CMSS13 put_in_hands(src) failed.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(combistick), Is.True,
                        "The local thrown item should not be stopped by the failed catch branch.");
                    Assert.That(damageable.TotalDamage, Is.GreaterThan(FixedPoint2.Zero),
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
    public async Task CallCombiLowPowerPullsWeaponButKeepsChainLikeCmss13Recall()
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
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new(6, 0)));
            var action = entMan.SpawnEntity("CMUActionYautjaCallCombi", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                EquipBracer(entMan, hunter, bracer);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 10;
                var chained = entMan.GetComponent<YautjaChainedWeaponComponent>(combistick);
                chained.LinkedTo = hunter;

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var call = new YautjaCallCombiActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(combistick, call);

                Assert.Multiple(() =>
                {
                    Assert.That(call.Handled, Is.True);
                    Assert.That(hands.IsHolding(hunter, combistick), Is.True,
                        "CMSS13 recall() attempts put_in_hands(src, TRUE) before drain_power(user, 70).");
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 10),
                        "CMSS13 drain_power failure leaves the bracer charge unchanged.");
                    Assert.That(chained.LinkedTo, Is.EqualTo(hunter),
                        "CMSS13 recall() returns TRUE before cleanup_chain() when drain_power fails after a hand pull.");
                    Assert.That(transform.GetMapCoordinates(combistick).MapId, Is.EqualTo(transform.GetMapCoordinates(hunter).MapId));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, bracer, combistick, action })
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

    private static void AssertScytheBonusStrike(
        IEntityManager entMan,
        EntityUid scythe,
        EntityUid user,
        EntityUid target,
        string source)
    {
        var scytheBonus = entMan.GetComponent<YautjaScytheBonusStrikeComponent>(scythe);
        var baseDamage = new DamageSpecifier
        {
            DamageDict = new()
            {
                { "Slash", 30 },
            },
        };

        Assert.That(scytheBonus.Chance, Is.EqualTo(0.15f),
            $"{source} uses prob(15) before the extra parent attack.");

        scytheBonus.Chance = 1f;
        var bonus = RaiseMeleeHit(entMan, scythe, user, target, baseDamage);

        scytheBonus.Chance = 0f;
        var noBonus = RaiseMeleeHit(entMan, scythe, user, target, baseDamage);

        scytheBonus.Chance = 1f;
        var miss = RaiseMeleeHit(entMan, scythe, user, target, baseDamage, isHit: false);

        Assert.Multiple(() =>
        {
            Assert.That(bonus.BonusDamage.GetTotal(), Is.EqualTo(baseDamage.GetTotal()),
                $"{source} calls ..() again when the source prob(15) succeeds.");
            Assert.That(noBonus.BonusDamage.GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                $"{source} does not add the extra strike when the source prob(15) fails.");
            Assert.That(miss.BonusDamage.GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                $"{source} only represents a real hit; examine/prediction damage queries must not trigger the bonus strike.");
        });
    }

    private static FixedPoint2 DamageTotal(DamageSpecifier damage)
    {
        var total = FixedPoint2.Zero;
        foreach (var value in damage.DamageDict.Values)
        {
            total += value;
        }

        return total;
    }

    [Test]
    public async Task HunterSpearStartsCmss13FishingDoAfterOnlyOnFishingSpots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var spear = entMan.SpawnEntity("CMUYautjaHunterSpear", map.GridCoords);
            var fishingSpot = entMan.SpawnEntity("FishingSpotWater", map.GridCoords.Offset(new(1, 0)));
            var ordinaryTarget = entMan.SpawnEntity("CMCrowbar", map.GridCoords.Offset(new(0, 1)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, spear), Is.True);

                var fishing = entMan.GetComponent<YautjaHunterSpearFishingComponent>(spear);

                var notFishing = StartSpearFishing(entMan, hunter, spear, ordinaryTarget, true);
                var notReachable = StartSpearFishing(entMan, hunter, spear, fishingSpot, false);
                var started = StartSpearFishing(entMan, hunter, spear, fishingSpot, true);
                var duplicate = StartSpearFishing(entMan, hunter, spear, fishingSpot, true);

                Assert.Multiple(() =>
                {
                    Assert.That(notFishing.Handled, Is.False,
                        "CMSS13 spear/afterattack() returns unless the clicked turf has fishing_allowed.");
                    Assert.That(notReachable.Handled, Is.False,
                        "CMSS13 spear/afterattack() requires proximity_flag before fishing.");
                    Assert.That(started.Handled, Is.True,
                        "CMSS13 spear/afterattack() handles reachable fishing_allowed turfs by starting do_after.");
                    Assert.That(duplicate.Handled, Is.False,
                        "CMSS13 spear/afterattack() refuses while busy_fishing is TRUE.");
                    Assert.That(ActiveSpearFishingDoAfters(entMan, hunter), Is.EqualTo(1));
                    Assert.That(fishing.BusyFishing, Is.True,
                        "CMSS13 sets busy_fishing = TRUE before do_after(user, 5 SECONDS, INTERRUPT_ALL, BUSY_ICON_HOSTILE).");
                    Assert.That(fishing.DoAfter, Is.EqualTo(TimeSpan.FromSeconds(5)),
                        "CMSS13 spear fishing uses do_after(user, 5 SECONDS, INTERRUPT_ALL, BUSY_ICON_HOSTILE).");
                    Assert.That(fishing.FailureChance, Is.EqualTo(0.60f),
                        "CMSS13 spear fishing has prob(60) failure because fishing rods are preferred.");
                    Assert.That((fishing.CommonWeight, fishing.UncommonWeight, fishing.RareWeight, fishing.UltraRareWeight),
                        Is.EqualTo((60, 15, 5, 1)),
                        "CMSS13 hunter spear passes common/uncommon/rare/ultra_rare weights of 60/15/5/1 to get_fishing_loot().");
                    Assert.That(ActiveSpearFishingDelay(entMan, hunter), Is.EqualTo(TimeSpan.FromSeconds(5)));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, spear, fishingSpot, ordinaryTarget })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CeremonialDaggerFlayingUsesCmss13DeadHumanoidGuards()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var weakTechUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(0, 1)));
            var aliveHuman = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var deadHuman = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            var deadXeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new(3, 0)));
            var deadYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new(4, 0)));
            var deadSynth = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(5, 0)));
            var dagger = entMan.SpawnEntity("CMUYautjaCeremonialDagger", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(weakTechUser);
                entMan.EnsureComponent<SynthComponent>(deadSynth);
                foreach (var uid in new[] { deadHuman, deadXeno, deadYautja, deadSynth })
                    mobState.ChangeMobState(uid, MobState.Dead);

                var alive = RaiseMeleeHit(entMan, dagger, hunter, aliveHuman);
                var nonHuman = RaiseMeleeHit(entMan, dagger, hunter, deadXeno);
                var weak = RaiseMeleeHit(entMan, dagger, weakTechUser, deadHuman);
                var yautja = RaiseMeleeHit(entMan, dagger, hunter, deadYautja);
                var synth = RaiseMeleeHit(entMan, dagger, hunter, deadSynth);
                var started = RaiseMeleeHit(entMan, dagger, hunter, deadHuman);
                var daggerComp = entMan.GetComponent<YautjaCeremonialDaggerComponent>(dagger);

                Assert.Multiple(() =>
                {
                    Assert.That(daggerComp.PrepareDelay, Is.EqualTo(TimeSpan.FromSeconds(1)),
                        "CMSS13 ceremonial dagger performs an initial do_after(user, 1 SECONDS, INTERRUPT_NO_NEEDHAND, BUSY_ICON_HOSTILE, victim).");
                    Assert.That(daggerComp.FlayDelay, Is.EqualTo(TimeSpan.FromSeconds(4)),
                        "CMSS13 ceremonial dagger then performs do_after(user, 4 SECONDS, INTERRUPT_NO_NEEDHAND, BUSY_ICON_HOSTILE, victim).");
                    Assert.That(alive.Handled, Is.False,
                        "CMSS13 /obj/item/weapon/yautja/knife/attack() returns parent melee when target.stat != DEAD.");
                    Assert.That(nonHuman.Handled, Is.True,
                        "CMSS13 ceremonial dagger handles dead non-humanoid targets with the humanoid-only warning instead of parent melee.");
                    Assert.That(weak.Handled, Is.True,
                        "CMSS13 requires TRAIT_SUPER_STRONG for flaying; local tech authorization alone must not start flaying.");
                    Assert.That(yautja.Handled, Is.True,
                        "CMSS13 refuses to flay Yautja victims with the ARE YOU OUT OF YOUR MIND branch.");
                    Assert.That(synth.Handled, Is.True,
                        "CMSS13 refuses to flay synth victims before starting the do_after.");
                    Assert.That(started.Handled, Is.True,
                        "CMSS13 starts the initial dead-human flay sequence instead of applying normal melee damage.");
                    Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.EqualTo(1));
                    Assert.That(SingleIncompleteDoAfterDelay(entMan, hunter), Is.EqualTo(TimeSpan.FromSeconds(1)),
                        "CMSS13 first waits do_after(user, 1 SECONDS, INTERRUPT_NO_NEEDHAND, BUSY_ICON_HOSTILE, victim) before the visible flaying work.");
                    Assert.That(ActiveIncompleteDoAfters(entMan, weakTechUser), Is.Zero);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, weakTechUser, aliveHuman, deadHuman, deadXeno, deadYautja, deadSynth, dagger })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CeremonialDaggerFlayingChainsCmss13OneAndFourSecondDoAfters()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid victim = default;
        EntityUid dagger = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
                dagger = entMan.SpawnEntity("CMUYautjaCeremonialDagger", map.GridCoords);
                mobState.ChangeMobState(victim, MobState.Dead);

                var started = RaiseMeleeHit(entMan, dagger, hunter, victim);

                Assert.Multiple(() =>
                {
                    Assert.That(started.Handled, Is.True);
                    Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.EqualTo(1));
                    Assert.That(SingleIncompleteDoAfterDelay(entMan, hunter), Is.EqualTo(TimeSpan.FromSeconds(1)));
                });
            });

            await server.WaitRunTicks(40);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.EqualTo(1),
                        "CMSS13 ceremonial dagger starts the second do_after after the one-second setup completes.");
                    Assert.That(SingleIncompleteDoAfterDelay(entMan, hunter), Is.EqualTo(TimeSpan.FromSeconds(4)),
                        "CMSS13 ceremonial dagger uses do_after(user, 4 SECONDS, INTERRUPT_NO_NEEDHAND, BUSY_ICON_HOSTILE, victim) for the first cutting pass.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, victim, dagger })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CeremonialDaggerFirstFlayPassAppliesCmss13StageOneDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid victim = default;
        EntityUid dagger = default;

        try
        {
            Dictionary<EntityUid, FixedPoint2> partHealthBefore = new();
            FixedPoint2 damageBefore = default;
            var partCount = 0;

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();
                var mobState = entMan.System<MobStateSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
                dagger = entMan.SpawnEntity("CMUYautjaCeremonialDagger", map.GridCoords);
                mobState.ChangeMobState(victim, MobState.Dead);
                damageBefore = entMan.GetComponent<DamageableComponent>(victim).TotalDamage;

                foreach (var (partUid, _) in body.GetBodyChildren(victim))
                {
                    if (!entMan.TryGetComponent<BodyPartHealthComponent>(partUid, out var health))
                        continue;

                    partHealthBefore[partUid] = health.Current;
                }

                partCount = partHealthBefore.Count;

                var started = RaiseMeleeHit(entMan, dagger, hunter, victim);

                Assert.That(started.Handled, Is.True);
            });

            await server.WaitRunTicks(180);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var flayed = entMan.GetComponent<YautjaFlayedComponent>(victim);
                var damageable = entMan.GetComponent<DamageableComponent>(victim);

                Assert.Multiple(() =>
                {
                    Assert.That(flayed.Stage, Is.EqualTo(1),
                        "CMSS13 add_flay_overlay(stage = 1) records the first prepared-skin stage after the first four-second cutting pass.");
                    Assert.That(flayed.CurrentFlayer, Is.Null,
                        "CMSS13 only keeps current_flayer during the active flay datum do_after; the first pass should finish before the recursive stage loop is ported.");
                    Assert.That(flayed.NextStage, Is.EqualTo(YautjaFlayingStage.Scalp),
                        "CMSS13 creates the flaying datum after the first cuts with FLAY_STAGE_SCALP as the next recursive stage.");
                    Assert.That(damageable.TotalDamage - damageBefore, Is.EqualTo((FixedPoint2) 15),
                        "CMSS13 applies 15 BRUTE to each limb during the first pass; local aggregate damage records one source pass while part health stores the per-limb ledger.");
                    Assert.That(partCount, Is.GreaterThan(0),
                        "The test fixture must expose local body parts before it can verify the CMSS13 per-limb damage loop.");
                    Assert.That(entMan.EntityQuery<YautjaScalpComponent>().Count(), Is.Zero,
                        "CMSS13 first pass only creates the flaying datum; the scalp appears on the next FLAY_STAGE_SCALP pass.");
                });

                foreach (var (partUid, before) in partHealthBefore)
                {
                    if (!entMan.TryGetComponent<BodyPartHealthComponent>(partUid, out var health))
                        continue;

                    Assert.That(health.Current, Is.EqualTo(before - (FixedPoint2) 15),
                        "CMSS13 loops over victim.limbs and applies 15 BRUTE to every limb during the first cutting pass.");
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, victim, dagger })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CeremonialDaggerScalpStageCreatesRuntimeScalpLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid victim = default;
        EntityUid dagger = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var metadata = entMan.System<MetaDataSystem>();
                var mobState = entMan.System<MobStateSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
                dagger = entMan.SpawnEntity("CMUYautjaCeremonialDagger", map.GridCoords);
                metadata.SetEntityName(victim, "Guan Thwei");
                mobState.ChangeMobState(victim, MobState.Dead);

                var flayed = entMan.EnsureComponent<YautjaFlayedComponent>(victim);
                flayed.Stage = 1;
                flayed.NextStage = YautjaFlayingStage.Scalp;

                var scalpStage = RaiseMeleeHit(entMan, dagger, hunter, victim);

                Assert.That(scalpStage.Handled, Is.True,
                    "CMSS13 flaying datum handles a resumed dead-human flay attempt at FLAY_STAGE_SCALP.");
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var flayed = entMan.GetComponent<YautjaFlayedComponent>(victim);
                var scalps = entMan.EntityQuery<YautjaScalpComponent, MetaDataComponent>().ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(flayed.NextStage, Is.EqualTo(YautjaFlayingStage.Strip),
                        "CMSS13 advances from FLAY_STAGE_SCALP to FLAY_STAGE_STRIP after tearing out the scalp.");
                    Assert.That(scalps.Count, Is.EqualTo(1),
                        "CMSS13 FLAY_STAGE_SCALP creates a runtime /obj/item/scalp.");
                    Assert.That(scalps[0].Item2.EntityName, Is.EqualTo("Guan Thwei's scalp"),
                        "Runtime scalps should be named from scalpee.real_name.");
                    Assert.That(scalps[0].Item2.EntityDescription, Is.Empty,
                        "Runtime scalp true_desc must not live in local metadata, because metadata examine text is visible to ordinary humans.");
                    Assert.That(scalps[0].Item1.TrueDescription, Does.Contain("This is the scalp of an irrelevant human."),
                        "A victim with no life kills and no huntdata biography uses the CMSS13 irrelevant-human true_desc branch.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, victim, dagger })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CeremonialDaggerAfterInteractFlaysDetachedLimbLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid dagger = default;
        EntityUid limb = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                dagger = entMan.SpawnEntity("CMUYautjaCeremonialDagger", map.GridCoords);
                limb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, dagger), Is.True);

                var bodyPart = entMan.GetComponent<BodyPartComponent>(limb);
                Assert.That(bodyPart.Body, Is.Null,
                    "The test fixture must model CMSS13 /obj/item/limb as a detached local body-part item.");

                var tooFar = StartDaggerLimbFlay(entMan, hunter, dagger, limb, false);
                var started = StartDaggerLimbFlay(entMan, hunter, dagger, limb, true);
                var duplicate = StartDaggerLimbFlay(entMan, hunter, dagger, limb, true);

                Assert.Multiple(() =>
                {
                    Assert.That(tooFar.Handled, Is.False,
                        "CMSS13 /obj/item/weapon/yautja/knife/afterattack() returns when proximity is false.");
                    Assert.That(started.Handled, Is.True,
                        "CMSS13 handles reachable detached limbs by starting the limb flay do_after.");
                    Assert.That(duplicate.Handled, Is.False,
                        "A duplicate detached-limb flay do_after should not be restarted while the first one is active.");
                    Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.EqualTo(1));
                    Assert.That(SingleIncompleteDoAfterDelay(entMan, hunter), Is.EqualTo(TimeSpan.FromSeconds(2)),
                        "CMSS13 detached limb flay uses do_after(user, 2 SECONDS, INTERRUPT_NO_NEEDHAND, BUSY_ICON_HOSTILE, current_limb).");
                    Assert.That(entMan.HasComponent<YautjaFlayedComponent>(limb), Is.False);
                });
            });

            await server.WaitRunTicks(90);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<YautjaFlayedComponent>(limb), Is.True,
                        "CMSS13 sets current_limb.flayed = TRUE after the detached-limb flay do_after completes.");
                    Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.Zero);
                });

                var repeat = StartDaggerLimbFlay(entMan, hunter, dagger, limb, true);

                Assert.Multiple(() =>
                {
                    Assert.That(repeat.Handled, Is.True,
                        "CMSS13 handles already-flayed limbs with a notice instead of falling through to generic interaction.");
                    Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.Zero,
                        "CMSS13 does not start a second do_after for current_limb.flayed limbs.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, dagger, limb })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CeremonialDaggerDetachedLimbFlayCancelUsesCmss13Notice()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid dagger = default;
        EntityUid limb = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();

                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                dagger = entMan.SpawnEntity("CMUYautjaCeremonialDagger", map.GridCoords);
                limb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, dagger), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var started = StartDaggerLimbFlay(entMan, hunter, dagger, limb, true);
                Assert.That(started.Handled, Is.True);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.EqualTo(1));
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var doAfterSystem = entMan.System<SharedDoAfterSystem>();
                var doAfterComp = entMan.GetComponent<DoAfterComponent>(hunter);
                var active = doAfterComp.DoAfters.Values.Single(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaCeremonialDaggerLimbFlayDoAfterEvent);

                doAfterSystem.Cancel(active.Id, doAfterComp);
            });

            await pair.ReallyBeIdle(10);

            await AssertClientHasPopup(client, "You decide not to flay left human arm.");

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<YautjaFlayedComponent>(limb), Is.False);
                    Assert.That(ActiveIncompleteDoAfters(entMan, hunter), Is.Zero);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();

                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, dagger, limb })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

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

    private static void AssertWeaponAppliesInterference(
        IEntityManager entMan,
        StatusEffectQuerySystem status,
        EntityUid weapon,
        EntityUid user,
        TimeSpan expected,
        string source)
    {
        var xeno = entMan.SpawnEntity("CMXenoDrone", entMan.GetComponent<TransformComponent>(user).Coordinates);

        try
        {
            Assert.That(status.TryGetTime(xeno, "YautjaInterference", out _), Is.False);

            RaiseMeleeHit(entMan, weapon, user, xeno);
            AssertInterferenceDuration(status, xeno, expected, source);
        }
        finally
        {
            if (!entMan.Deleted(xeno))
                entMan.DeleteEntity(xeno);
        }
    }

    private static void AssertWeaponDoesNotApplyInterference(
        IEntityManager entMan,
        StatusEffectQuerySystem status,
        EntityUid weapon,
        EntityUid user,
        string source)
    {
        var xeno = entMan.SpawnEntity("CMXenoDrone", entMan.GetComponent<TransformComponent>(user).Coordinates);

        try
        {
            RaiseMeleeHit(entMan, weapon, user, xeno);
            Assert.That(status.TryGetTime(xeno, "YautjaInterference", out _), Is.False, source);
        }
        finally
        {
            if (!entMan.Deleted(xeno))
                entMan.DeleteEntity(xeno);
        }
    }

    private static void AssertInterferenceDuration(
        StatusEffectQuerySystem status,
        EntityUid target,
        TimeSpan expected,
        string source)
    {
        Assert.That(status.TryGetTime(target, "YautjaInterference", out var time), Is.True, source);
        Assert.That(time!.Value.Item2 - time.Value.Item1, Is.EqualTo(expected), source);
    }

    private static AfterInteractEvent StartSpearFishing(
        IEntityManager entMan,
        EntityUid hunter,
        EntityUid spear,
        EntityUid target,
        bool canReach)
    {
        var interact = new AfterInteractEvent(
            hunter,
            spear,
            target,
            entMan.GetComponent<TransformComponent>(target).Coordinates,
            canReach);
        entMan.EventBus.RaiseLocalEvent(spear, interact);
        return interact;
    }

    private static AfterInteractEvent StartDaggerLimbFlay(
        IEntityManager entMan,
        EntityUid hunter,
        EntityUid dagger,
        EntityUid limb,
        bool canReach)
    {
        var interact = new AfterInteractEvent(
            hunter,
            dagger,
            limb,
            entMan.GetComponent<TransformComponent>(limb).Coordinates,
            canReach);
        entMan.EventBus.RaiseLocalEvent(dagger, interact);
        return interact;
    }

    private static int ActiveSpearFishingDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaHunterSpearFishingDoAfterEvent)
            : 0;
    }

    private static TimeSpan ActiveSpearFishingDelay(IEntityManager entMan, EntityUid user)
    {
        return entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(active =>
            !active.Cancelled &&
            !active.Completed &&
            active.Args.Event is YautjaHunterSpearFishingDoAfterEvent).Args.Delay;
    }

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

    private static string? FindHeldHand(IEntityManager entMan, EntityUid user, EntityUid item)
    {
        var hands = entMan.System<SharedHandsSystem>();
        var component = entMan.GetComponent<HandsComponent>(user);

        foreach (var hand in component.SortedHands)
        {
            if (hands.TryGetHeldItem((user, component), hand, out var held) && held == item)
                return hand;
        }

        return null;
    }

    private static ThrowItemAttemptEvent RaiseThrowItemAttempt(IEntityManager entMan, EntityUid item, EntityUid user)
    {
        var attempt = new ThrowItemAttemptEvent(user);
        entMan.EventBus.RaiseLocalEvent(item, ref attempt);
        return attempt;
    }

    private static int ActiveChainedUntangleDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaChainedWeaponUntangleDoAfterEvent)
            : 0;
    }

    private static TimeSpan ActiveChainedUntangleDelay(IEntityManager entMan, EntityUid user)
    {
        return entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(active =>
            !active.Cancelled &&
            !active.Completed &&
            active.Args.Event is YautjaChainedWeaponUntangleDoAfterEvent).Args.Delay;
    }

    private static int ActiveIncompleteDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed)
            : 0;
    }

    private static TimeSpan SingleIncompleteDoAfterDelay(IEntityManager entMan, EntityUid user)
    {
        return entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(active =>
            !active.Cancelled &&
            !active.Completed).Args.Delay;
    }

    private static async Task AssertClientHasPopup(
        RobustIntegrationTest.ClientIntegrationInstance client,
        string expected)
    {
        await client.WaitAssertion(() =>
        {
            var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
            Assert.That(labels, Does.Contain(expected), $"Actual popups:\n{string.Join("\n", labels)}");
        });
    }
}
