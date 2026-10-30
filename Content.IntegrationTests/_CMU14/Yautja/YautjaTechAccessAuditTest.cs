using Content.CMU.Server.Yautja;
using Content.Server.CMU14.Yautja;
using Content.Shared.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

// CMU14 Test: the Yautja item-usage audit against CM-SS13.
//
// Reference behavior (cmss13-devs/cmss13 @ 3753130, code/):
//  - code/game/objects/items.dm:183 — flags_item = ITEM_PREDATOR only does AddElement(yautja_tracked_item).
//    It never restricts carrying, swinging or throwing; the items' own pickup()/equipped() overrides only
//    print flavour text (yaut_bracers.dm:87 "The bracer feels cold against your skin...", yaut_mask.dm:64,
//    yaut_items.dm:243), so those stay unrestricted for every species.
//  - Every real restriction is a per-function HAS_TRAIT(user, TRAIT_YAUTJA_TECH) check, with
//    hunter_data.thralled counting as authorized: guns via able_to_fire (yaut_weapons.dm:1372/1467/1571,
//    mcaste_weapons.dm:69/162/257), the bracer menu/verbs (yaut_bracers.dm:535/1513/1551), the chain's
//    attack_hand (yaut_weapons.dm:724), the smart disc's attack_self fiddle (smartdisc.dm:62-80) and the
//    falcon's can_control_falcon_drone (falcon.dm:55-70, which also demands worn yautja bracers).
[TestFixture]
public sealed class YautjaTechAccessAuditTest
{
    private const string GlovesSlot = "gloves";
    private const string EarsSlot = "ears";

    // smartdisc.dm:68-71 — a non-Yautja activating the disc has a 75% chance of "You fiddle with the disc,
    // but nothing happens." and otherwise arms it. Repeated independent attempts make both branches certain
    // to appear without depending on one specific random draw.
    private const int ActivationAttempts = 64;

    private static MapCoordinates TestCoords(IEntityManager entMan)
    {
        var maps = entMan.System<SharedMapSystem>();
        maps.CreateMap(out var mapId, runMapInit: true);
        return new MapCoordinates(default, mapId);
    }

    private static bool PickupAttempt(IEntityManager entMan, EntityUid user, EntityUid item)
    {
        var attempt = new GettingPickedUpAttemptEvent(user, item, false);
        entMan.EventBus.RaiseLocalEvent(item, attempt);
        return attempt.Cancelled;
    }

    private static bool ShootAttempt(IEntityManager entMan, EntityUid user, EntityUid gun)
    {
        var coords = entMan.GetComponent<TransformComponent>(gun).Coordinates;
        var attempt = new AttemptShootEvent(user, null, coords, coords);
        entMan.EventBus.RaiseLocalEvent(gun, ref attempt);
        return attempt.Cancelled;
    }

    private static bool TryArmDisc(IEntityManager entMan, EntityUid user, EntityUid disc)
    {
        return entMan.System<ItemToggleSystem>().TrySetActive((disc, null), true, user, false);
    }

    [Test]
    public async Task NonYautjaMayCarryAndSwingYautjaGearLikeCmss13ItemPredator()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var sword = entMan.SpawnEntity("CMUYautjaClanSword", coords);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", coords);

            // items.dm:183 attaches a tracker element, so nothing cancels the pickup attempt.
            Assert.That(PickupAttempt(entMan, marine, sword), Is.False,
                "CM-SS13 ITEM_PREDATOR does not block picking up Yautja weapons.");
            Assert.That(PickupAttempt(entMan, marine, rifle), Is.False,
                "CM-SS13 lets anyone carry a stolen Yautja gun; only firing is gated.");

            var hands = entMan.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(marine, sword, checkActionBlocker: false), Is.True);

            var tech = entMan.GetComponent<YautjaTechItemComponent>(sword);
            Assert.Multiple(() =>
            {
                Assert.That(tech.BlockPickup, Is.False, "CMU14: no CM-SS13 counterpart for a pickup block.");
                Assert.That(tech.BlockMelee, Is.False, "CMU14: CM-SS13 never blocks swinging Yautja gear.");
                Assert.That(tech.BlockThrow, Is.False, "CMU14: CM-SS13 never blocks throwing Yautja gear.");
                Assert.That(tech.BlockShoot, Is.True, "CMU14: CM-SS13 keeps able_to_fire gated on tech users.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyYautjaTechMayFireYautjaGunsLikeCmss13AbleToFire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var hunter = entMan.SpawnEntity("CMUMobYautja", coords);
            var marineRifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", coords);
            var hunterRifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", coords);

            Assert.That(ShootAttempt(entMan, marine, marineRifle), Is.True,
                "yaut_weapons.dm:1467 able_to_fire refuses a non-tech user.");
            Assert.That(ShootAttempt(entMan, hunter, hunterRifle), Is.False,
                "A Yautja fires its own gun normally.");

            var spike = entMan.SpawnEntity("CMUYautjaSpikeLauncher", coords);
            Assert.That(ShootAttempt(entMan, marine, spike), Is.True,
                "yaut_weapons.dm:1372 able_to_fire on the spike launcher.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaDiscActivationUsesCmss13FiddleChance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var hunter = entMan.SpawnEntity("CMUMobYautja", coords);

            var armed = 0;
            for (var i = 0; i < ActivationAttempts; i++)
            {
                // A fresh disc per attempt: arming one makes it active, and the gate is per activation.
                var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", coords);
                if (TryArmDisc(entMan, marine, disc))
                    armed++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(armed, Is.GreaterThan(0),
                    "smartdisc.dm:68-71 leaves a 25% chance that a non-Yautja arms the disc.");
                Assert.That(armed, Is.LessThan(ActivationAttempts),
                    "smartdisc.dm:68-71 fiddles 75% of the time instead of arming.");
            });

            var hunterDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", coords);
            Assert.That(TryArmDisc(entMan, hunter, hunterDisc), Is.True,
                "smartdisc.dm:26 isyautja(user) arms the disc for a Yautja without the fiddle.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnthralledUsersCountAsYautjaTechLikeCmss13Thralled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = TestCoords(entMan);

            // yaut_hudprocs.dm:526 grants TRAIT_YAUTJA_TECH to a newblood, and the checks that read the trait
            // ("!HAS_TRAIT(...) && !hunter_data.thralled") pass for an authorized user. Note the smart disc is
            // not one of them: smartdisc.dm:26/68 uses isyautja(), a species test, so a thrall still fiddles.
            var thrall = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaTechAuthorizedComponent>(thrall);

            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", coords);
            Assert.That(ShootAttempt(entMan, thrall, rifle), Is.False,
                "An authorized user is not refused by able_to_fire.");

            var spike = entMan.SpawnEntity("CMUYautjaSpikeLauncher", coords);
            Assert.That(ShootAttempt(entMan, thrall, spike), Is.False,
                "yaut_weapons.dm:1372 able_to_fire on the spike launcher also passes for an authorized user.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnyoneMayWearTheFalconDroneAndOrdinaryHumansMayControlItWithBracers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var actions = entMan.System<ActionContainerSystem>();
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var drone = entMan.SpawnEntity("CMUYautjaFalconDrone", coords);

            // falcon.dm:42-51 grants the control verb on equip to whoever wears it, and gates the use.
            Assert.That(inventory.TryEquip(marine, drone, EarsSlot, silent: true, force: true), Is.True,
                "CM-SS13 falcon_drone/equipped has no species check.");
            Assert.That(PickupAttempt(entMan, marine, drone), Is.False,
                "CM-SS13 ITEM_PREDATOR does not block picking the drone up either.");

            var droneComp = entMan.GetComponent<YautjaFalconDroneComponent>(drone);
            var getActions = new GetItemActionsEvent(actions, marine, drone, SlotFlags.EARS);
            entMan.EventBus.RaiseLocalEvent(drone, getActions);
            var actionIds = new List<string>();
            foreach (var action in getActions.Actions)
                actionIds.Add(entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID ?? string.Empty);

            Assert.That(actionIds, Does.Contain(droneComp.ControlActionId.Id),
                "falcon.dm:42 hands the Control Falcon Drone action to any wearer.");

            // CMU14: deliberate divergence from CM-SS13 here - an ordinary human may control the drone. The bracer
            // requirement is still enforced (falcon.dm:64 demands worn yautja bracers), so without one the use is
            // refused instead of handing the drone over.
            entMan.EventBus.RaiseLocalEvent(drone, new UseInHandEvent(marine));
            Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(marine), Is.False,
                "Without the required bracers the drone is not handed over.");

            var bracer = entMan.SpawnEntity("CMUYautjaBracer", coords);
            Assert.That(inventory.TryEquip(marine, bracer, GlovesSlot, silent: true, force: true), Is.True,
                "falcon.dm:64 demands worn yautja bracers to control the drone.");

            entMan.EventBus.RaiseLocalEvent(drone, new UseInHandEvent(marine));
            Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(marine), Is.True,
                "CMU14: an ordinary human wearing bracers deploys and controls the drone.");

            // A Yautja works exactly as before.
            var hunter = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            var hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", coords);
            var hunterDrone = entMan.SpawnEntity("CMUYautjaFalconDrone", coords);
            Assert.That(inventory.TryEquip(hunter, hunterBracer, GlovesSlot, silent: true, force: true), Is.True);

            entMan.EventBus.RaiseLocalEvent(hunterDrone, new UseInHandEvent(hunter));
            Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.True,
                "A Yautja wearing bracers deploys and controls the drone.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyYautjaTechMaySpeakOnThePackChannelLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var comms = entMan.System<YautjaCommsAccessSystem>();
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var hunter = entMan.SpawnEntity("CMUMobYautja", coords);
            var thrall = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaTechAuthorizedComponent>(thrall);

            // yaut_items.dm:526 — "if(!isyautja(M) && !isthrall(M)) //Nope." on the yautja headset. The
            // send attempt itself also has to clear CMU's own relay/range simulation, so the gate's
            // decision is asserted directly; YautjaCommsAccessSystem.OnRadioSendAttempt cancels the send
            // on the CMUYautja channel for exactly the speakers this returns false for.
            Assert.Multiple(() =>
            {
                Assert.That(comms.CanUsePackComms(hunter), Is.True, "A Yautja speaks on the pack channel.");
                Assert.That(comms.CanUsePackComms(thrall), Is.True, "An authorized thrall speaks on the pack channel.");
                Assert.That(comms.CanUsePackComms(marine), Is.False, "A plain human may not speak on it.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AuthorizedUsersMayFitMaskOrnamentsLikeCmss13Thralls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = TestCoords(entMan);

            // yaut_mask.dm:128 refuses mask functions for "!HAS_TRAIT(user, TRAIT_YAUTJA_TECH) && !thralled".
            var mask = entMan.SpawnEntity("CMUYautjaMask", coords);
            var click = entMan.GetComponent<TransformComponent>(mask).Coordinates;
            entMan.EnsureComponent<YautjaMaskAccessoryHolderComponent>(mask);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var marineOrnament = entMan.SpawnEntity("CMUYautjaMaskOrnament", coords);
            var refused = new InteractUsingEvent(marine, marineOrnament, mask, click);
            entMan.EventBus.RaiseLocalEvent(mask, refused);
            Assert.Multiple(() =>
            {
                Assert.That(refused.Handled, Is.False, "A plain human may not fit an ornament.");
                Assert.That(entMan.GetComponent<TransformComponent>(marineOrnament).ParentUid, Is.Not.EqualTo(mask),
                    "The ornament stays out of the mask for an unauthorized user.");
            });

            var thrall = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaTechAuthorizedComponent>(thrall);
            var thrallOrnament = entMan.SpawnEntity("CMUYautjaMaskOrnament", coords);
            var accepted = new InteractUsingEvent(thrall, thrallOrnament, mask, click);
            entMan.EventBus.RaiseLocalEvent(mask, accepted);
            Assert.Multiple(() =>
            {
                Assert.That(accepted.Handled, Is.True,
                    "An authorized user counts as a tech user, exactly like an enthralled thrall in CM-SS13.");
                Assert.That(entMan.GetComponent<TransformComponent>(thrallOrnament).ParentUid, Is.EqualTo(mask),
                    "The ornament ends up installed in the mask.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyYautjaCanOpenTheMarkAndEmotePanels()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;

        EntityUid marine = default;
        EntityUid hunter = default;
        EntityUid marineBracer = default;
        EntityUid hunterBracer = default;
        EntityUid? previousAttached = null;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var coords = TestCoords(entMan);
            var session = server.PlayerMan.Sessions.Single();
            previousAttached = session.AttachedEntity;

            marine = entMan.SpawnEntity("CMMobHuman", coords);
            // The spawned CMUMobYautja already wears its own gear, so the bracer cannot be equipped on it;
            // a human granted YautjaComponent is the recipe the other bracer/falcon fixtures use.
            hunter = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            marineBracer = entMan.SpawnEntity("CMUYautjaBracer", coords);
            hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", coords);
            server.PlayerMan.SetAttachedEntity(session, marine);

            Assert.That(inventory.TryEquip(marine, marineBracer, GlovesSlot, silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(hunter, hunterBracer, GlovesSlot, silent: true, force: true), Is.True);
        });

        // CM-SS13 keeps these on the yautja HUD (yaut_actions.dm / yaut_hudprocs.dm) and both handlers are
        // keyed to the yautja tech holder: Open Mark Panel through YautjaMarkSystem.CanUsePanel's
        // HasComp<YautjaComponent> test, Open Emote Panel by subscribing on YautjaComponent itself.
        // The alternate path is raising the action directly on the bracer, which must do nothing.
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.EventBus.RaiseLocalEvent(marineBracer, new YautjaOpenMarkPanelActionEvent { Performer = marine });
            entMan.EventBus.RaiseLocalEvent(marine, new YautjaAudioPanelActionEvent { Performer = marine });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<SharedUserInterfaceSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(ui.IsUiOpen(marineBracer, YautjaMarkUIKey.Key, marine), Is.False,
                    "A human cannot open the mark panel, even wearing the bracer.");
                Assert.That(ui.IsUiOpen(marineBracer, YautjaAudioPanelUIKey.Key, marine), Is.False,
                    "A human cannot open the emote panel.");
            });
        });

        await server.WaitPost(() =>
        {
            var session = server.PlayerMan.Sessions.Single();
            server.PlayerMan.SetAttachedEntity(session, hunter);
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.EventBus.RaiseLocalEvent(hunterBracer, new YautjaOpenMarkPanelActionEvent { Performer = hunter });
            entMan.EventBus.RaiseLocalEvent(hunter, new YautjaAudioPanelActionEvent { Performer = hunter });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<SharedUserInterfaceSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(ui.IsUiOpen(hunterBracer, YautjaMarkUIKey.Key, hunter), Is.True,
                    "A Yautja keeps the mark panel.");
                Assert.That(ui.IsUiOpen(hunterBracer, YautjaAudioPanelUIKey.Key, hunter), Is.True,
                    "A Yautja keeps the emote panel.");
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var session = server.PlayerMan.Sessions.Single();
            server.PlayerMan.SetAttachedEntity(session, previousAttached);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconControlStartsImmediatelyWithoutChanceLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var coords = TestCoords(entMan);

            // falcon.dm:55-70: human + TRAIT_YAUTJA_TECH + worn bracers, and control_falcon_drone() runs
            // straight away - no do_after and no prob() anywhere on the path.
            var hunter = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", coords);
            Assert.That(inventory.TryEquip(hunter, bracer, GlovesSlot, silent: true, force: true), Is.True);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var drone = entMan.SpawnEntity("CMUYautjaFalconDrone", coords);
                var control = new YautjaFalconControlActionEvent { Performer = hunter };
                entMan.EventBus.RaiseLocalEvent(drone, control);

                Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.True,
                    $"attempt {attempt}: drone control must engage on the spot, with no failure roll");

                var controller = entMan.GetComponent<YautjaFalconControllerComponent>(hunter);
                if (!entMan.Deleted(controller.Drone))
                    entMan.DeleteEntity(controller.Drone);

                entMan.RemoveComponent<YautjaFalconControllerComponent>(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ToggleIdChipRefusesNonYautjaLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var utility = entMan.System<YautjaBracerUtilitySystem>();
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var bracerComp = entMan.SpawnEntity("CMUYautjaBracer", coords);
            Assert.That(inventory.TryEquip(marine, bracerComp, GlovesSlot, silent: true, force: true), Is.True);

            // yaut_bracers.dm:1503-1515: a plain tech check that answers "You do not know how to use this."
            // and returns - no misuse do_after, no shock, nothing else.
            var bracer = (bracerComp, entMan.GetComponent<YautjaBracerComponent>(bracerComp));
            Assert.That(utility.TryToggleIdChip(bracer, marine), Is.False,
                "A non-Yautja is refused outright rather than being sent into the misuse roll.");

            var hunter = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            var hunterBracerComp = entMan.SpawnEntity("CMUYautjaBracer", coords);
            Assert.That(inventory.TryEquip(hunter, hunterBracerComp, GlovesSlot, silent: true, force: true), Is.True);
            var hunterBracer = (hunterBracerComp, entMan.GetComponent<YautjaBracerComponent>(hunterBracerComp));
            Assert.That(utility.TryToggleIdChip(hunterBracer, hunter), Is.True,
                "A Yautja keeps the button.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerCanBeTakenAndWornButItsFunctionsStayGated()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", coords);

            // yaut_bracers.dm:87 pickup() prints "The bracer feels cold against your skin..." and returns,
            // so equipping is unrestricted while the menu/verbs stay behind TRAIT_YAUTJA_TECH
            // (yaut_bracers.dm:535/1513/1551).
            Assert.That(PickupAttempt(entMan, marine, bracer), Is.False,
                "CM-SS13 lets a human take a bracer off a dead Yautja.");
            Assert.That(inventory.TryEquip(marine, bracer, GlovesSlot, silent: true, force: true), Is.True,
                "yaut_bracers.dm:62 equipped() only sets up the wearer's features, it does not refuse.");

            var tech = entMan.GetComponent<YautjaTechItemComponent>(bracer);
            // CMU14: the bracer deliberately does not block in-hand use (blockUse: false), so a non-tech user
            // reaches the bracer's own misuse path - that is where the shock and delimb consequences live
            // (YautjaBracerUtilitySystem), which is the reference's model of gating the functions rather than the
            // item. Blocking use here would substitute a plain refusal popup for those mechanics.
            Assert.That(tech.BlockUse, Is.False,
                "The bracer's functions are gated by the bracer systems and the misuse roll, not by a use block.");
        });

        await pair.CleanReturnAsync();
    }

    // Regression: the reported bug was that a Yautja wearing a bracer with nothing fitted still received Deploy
    // Wristblades, Deploy Scimitar and Deploy Shield, because the buttons were granted from ActionWhitelist
    // membership alone. The rule the prototypes document (YautjaComponents.GetDefaultGearPrototypes: a bracer
    // pre-installs only its plasma caster and other attachments "have to be installed explicitly") is that a
    // button follows its attachment, so this covers both the absent and the installed case.
    [Test]
    public async Task GearButtonsFollowTheInstalledAttachmentsLikeCmss13BracerActions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var actions = entMan.System<ActionContainerSystem>();
            var coords = TestCoords(entMan);

            var hunter = entMan.SpawnEntity("CMMobHuman", coords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", coords);
            Assert.That(inventory.TryEquip(hunter, bracer, GlovesSlot, silent: true, force: true), Is.True);

            List<string> ActionIds()
            {
                var getActions = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, getActions);
                var ids = new List<string>();
                foreach (var action in getActions.Actions)
                    ids.Add(entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID ?? string.Empty);
                return ids;
            }

            // A fresh bracer carries only its pre-installed plasma caster.
            var bare = ActionIds();
            Assert.Multiple(() =>
            {
                Assert.That(bare, Does.Contain("CMUActionYautjaToggleCaster"),
                    "the pre-installed caster still grants the plasmacaster action.");
                Assert.That(bare, Does.Not.Contain("CMUActionYautjaToggleWristBlades"),
                    "no wristblades are fitted, so there must be no Deploy Wristblades action.");
                Assert.That(bare, Does.Not.Contain("CMUActionYautjaToggleScimitar"),
                    "no scimitar is fitted, so there must be no Deploy Scimitar action.");
                Assert.That(bare, Does.Not.Contain("CMUActionYautjaToggleShield"),
                    "no shield is fitted, so there must be no Deploy Shield action.");
                Assert.That(bare, Does.Not.Contain("CMUActionYautjaToggleChainGauntlet"),
                    "no chain gauntlet is fitted, so there must be no Deploy Chain Gauntlet action.");
            });

            // Fitting an attachment grants exactly that button; uninstalling takes it away again.
            var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
            var wristBlades = entMan.SpawnEntity("CMUYautjaWristBladesAttachment", coords);
            gear.Gear[YautjaGearKind.WristBlades] = wristBlades;
            gear.InstalledGear.Add(wristBlades);

            Assert.That(ActionIds(), Does.Contain("CMUActionYautjaToggleWristBlades"),
                "a fitted wristblade attachment grants its button.");

            gear.InstalledGear.Remove(wristBlades);
            Assert.That(ActionIds(), Does.Not.Contain("CMUActionYautjaToggleWristBlades"),
                "removing the attachment takes the stale button away.");
        });

        await pair.CleanReturnAsync();
    }

}
