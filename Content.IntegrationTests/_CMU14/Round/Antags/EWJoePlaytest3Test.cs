using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Weapons.GunSwitch;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Playtest 3: single-target Test of Faith replaced by Judgement, the bio-mask style optical zoom,
/// per-kind Scour buffs with their alerts and the scoured scout cloak, and the Joe's own status panel.
/// (Overclock, Coolant, Unwind and the Z-leap live in <see cref="EWJoeHeatAbilitiesTest"/>.)
/// </summary>
[TestFixture]
public sealed class EWJoePlaytest3Test
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Baton = "CMStunbaton";
    private const string Armor = "CMArmorM3Medium";
    private const string ScoutCloak = "RMCBackpackScout";
    private const string YautjaCloak = "CMUYautjaCloakPack";
    private const string HolsterBelt = "RMCBeltHolsterPistol";
    private const string Pistol = "RMCWeaponPistolB92FS";
    private const string Backpack = "CMBackpackMarine";

    [Test]
    public async Task TestOfFaithTakesOneFacingTargetAndJudgementReplacesIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ability = entities.System<EWJoeAbilitySystem>();
            var facing = entities.System<SharedDirectionalAttackBlockSystem>();
            var rule = entities.System<EWJoeRuleSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 2));

            EWJoeTestHelpers.Face(entities, joe, human);
            EWJoeTestHelpers.Face(entities, human, joe);
            Assert.That(facing.IsFacingTarget(joe, human) && facing.IsFacingTarget(human, joe), "setup: they face each other");
            Assert.That(ability.CanTestFaith((joe, comp), human, out _), "a mutual gaze qualifies");

            var tof = new EWJoeTestOfFaithActionEvent { Performer = joe, Target = human };
            entities.EventBus.RaiseLocalEvent(joe, tof);
            Assert.That(tof.Handled, "the chosen target is tested");

            // looking away refuses it and leaves the action unspent
            var away = entities.System<SharedTransformSystem>();
            away.SetWorldRotation(human, away.GetWorldRotation(human) + Angle.FromDegrees(180));
            Assert.That(ability.CanTestFaith((joe, comp), human, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("cmu-ew-joe-test-of-faith-not-facing"));
            var refused = new EWJoeTestOfFaithActionEvent { Performer = joe, Target = human };
            entities.EventBus.RaiseLocalEvent(joe, refused);
            Assert.That(refused.Handled, Is.False);

            // Judgement's level takes Test of Faith away
            rule.ApplyLevel((joe, comp), 1);
            Assert.That(GrantedPrototypes(entities, comp), Does.Contain("CMUActionEWJoeTestOfFaith"));
            rule.ApplyLevel((joe, comp), 2);
            var granted = GrantedPrototypes(entities, comp);
            Assert.That(granted, Does.Not.Contain("CMUActionEWJoeTestOfFaith"), "Test of Faith is gone at level 2");
            Assert.That(granted, Does.Contain("CMUActionEWJoeJudgement"), "Judgement replaces it");
        });

        await pair.CleanReturnAsync();
    }

    private static List<string?> GrantedPrototypes(IEntityManager entities, EWJoeComponent comp)
    {
        return comp.GrantedActions
            .Where(a => entities.EntityExists(a) && !entities.IsQueuedForDeletion(a))
            .Select(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID)
            .ToList();
    }

    [Test]
    public async Task OpticalZoomWidensAndOffsetsTheViewUntilToggledOrStoodDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            rule.ApplyLevel((joe, comp), 0);

            var zoom = entities.GetComponent<EWJoeOpticalZoomComponent>(joe);
            var action = comp.GrantedActions.First(a =>
                entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "CMUActionEWJoeOpticalZoom");
            var actionComp = entities.GetComponent<ActionComponent>(action);
            var eye = entities.GetComponent<ContentEyeComponent>(joe);

            var on = new EWJoeOpticalZoomActionEvent { Performer = joe, Action = (action, actionComp) };
            entities.EventBus.RaiseLocalEvent(joe, on);
            Assert.Multiple(() =>
            {
                Assert.That(on.Handled);
                Assert.That(zoom.Zoomed, "zoomed in");
                Assert.That(eye.TargetZoom.X, Is.EqualTo(zoom.ZoomLevel).Within(0.01f), "the view widens like the bio-mask");
                Assert.That(zoom.Offset.Length(), Is.GreaterThan(1f), "and is pushed toward the facing direction");
            });

            var off = new EWJoeOpticalZoomActionEvent { Performer = joe, Action = (action, actionComp) };
            entities.EventBus.RaiseLocalEvent(joe, off);
            Assert.That(zoom.Zoomed, Is.False);
            Assert.That(eye.TargetZoom.X, Is.EqualTo(1f).Within(0.01f), "toggled off: normal view");

            // stand-down while zoomed puts the eye back
            var again = new EWJoeOpticalZoomActionEvent { Performer = joe, Action = (action, actionComp) };
            entities.EventBus.RaiseLocalEvent(joe, again);
            entities.RemoveComponent<EWJoeComponent>(joe);
            Assert.That(entities.HasComponent<EWJoeOpticalZoomComponent>(joe), Is.False);
            Assert.That(eye.TargetZoom.X, Is.EqualTo(1f).Within(0.01f), "stand-down resets the zoom");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScourKindsExpireSeparatelyWithAlertsAndTheCloakIsTheJoes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, cloakAction = default;
        await server.WaitAssertion(() =>
        {
            var scourSys = entities.System<EWJoeScourSystem>();
            var alerts = entities.System<AlertsSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            var baton = entities.SpawnEntity(Baton, map.GridCoords);
            var armor = entities.SpawnEntity(Armor, map.GridCoords);
            var cloakItem = entities.SpawnEntity(ScoutCloak, map.GridCoords);
            Assert.That(scourSys.Classify(cloakItem), Is.EqualTo(EWJoeScourKind.Cloak), "a scout cloak is food");

            scourSys.Scour((joe, comp), baton, EWJoeScourKind.Baton);
            scourSys.Scour((joe, comp), armor, EWJoeScourKind.Armor);
            scourSys.Scour((joe, comp), cloakItem, EWJoeScourKind.Cloak);

            var scour = entities.GetComponent<EWJoeScourComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(scour.Expiries.Keys, Is.EquivalentTo(new[] { EWJoeScourKind.Baton, EWJoeScourKind.Armor, EWJoeScourKind.Cloak }));
                Assert.That(alerts.IsShowingAlert(joe, EWJoeScourSystem.Alerts[EWJoeScourKind.Baton]), "baton alert shown");
                Assert.That(alerts.IsShowingAlert(joe, EWJoeScourSystem.Alerts[EWJoeScourKind.Armor]), "armor alert shown");
                Assert.That(alerts.IsShowingAlert(joe, EWJoeScourSystem.Alerts[EWJoeScourKind.Cloak]), "cloak alert shown");
            });

            // the cloak is the Joe's own: its whitelist lets the Joe use the RMC cloak action
            var cloak = entities.GetComponent<ThermalCloakComponent>(joe);
            Assert.That(entities.HasComponent<EntityTurnInvisibleComponent>(joe), "the wearer half of the cloak is on the Joe");
            Assert.That(cloak.Action, Is.Not.Null, "the cloak action was granted");
            cloakAction = cloak.Action!.Value;
            var toggle = new ThermalCloakTurnInvisibleActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, toggle);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(joe), "the Joe cloaks like a scout");

            // RMC's forced decloaks (spray, acid, devour) find the Joe's own cloak like a worn one
            Assert.That(entities.System<ThermalCloakSystem>().FindWornCloak(joe)?.Owner, Is.EqualTo(joe));

            // the yautja harness is not a scout cloak
            var yautjaCloak = entities.SpawnEntity(YautjaCloak, map.GridCoords);
            Assert.That(scourSys.Classify(yautjaCloak), Is.Null, "a yautja cloak is not scout gear");

            // the baton and the cloak run out; the armor keeps its own timer
            scour.Expiries[EWJoeScourKind.Baton] = TimeSpan.Zero;
            scour.Expiries[EWJoeScourKind.Cloak] = TimeSpan.Zero;
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var alerts = entities.System<AlertsSystem>();
            var scour = entities.GetComponent<EWJoeScourComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(scour.Expiries.Keys, Is.EquivalentTo(new[] { EWJoeScourKind.Armor }), "only the armor is left");
                Assert.That(scour.PunchStamina, Is.Zero, "baton bonus gone");
                Assert.That(scour.BulletArmor, Is.GreaterThan(0), "armor bonus still there");
                Assert.That(alerts.IsShowingAlert(joe, EWJoeScourSystem.Alerts[EWJoeScourKind.Baton]), Is.False, "baton alert cleared");
                Assert.That(alerts.IsShowingAlert(joe, EWJoeScourSystem.Alerts[EWJoeScourKind.Armor]), "armor alert stays");
                Assert.That(entities.HasComponent<ThermalCloakComponent>(joe), Is.False, "cloak gone");
                Assert.That(entities.HasComponent<EntityTurnInvisibleComponent>(joe), Is.False, "the wearer half is gone too");
                Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(joe), Is.False, "the Joe was uncloaked");
                Assert.That(!entities.EntityExists(cloakAction) || entities.IsQueuedForDeletion(cloakAction), "the cloak action is gone");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnwindDrawsHolsteredSidearmsButNotPackedGuns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var arsenal = entities.System<EWJoeArsenalSystem>();
            var inventory = entities.System<InventorySystem>();
            var containers = entities.System<SharedContainerSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var marine = entities.SpawnEntity(Human, map.GridCoords);

            var belt = entities.SpawnEntity(HolsterBelt, map.GridCoords);
            var pistol = entities.SpawnEntity(Pistol, map.GridCoords);
            Assert.That(containers.Insert(pistol, entities.GetComponent<StorageComponent>(belt).Container), "pistol holstered");
            Assert.That(inventory.TryEquip(marine, belt, "belt", silent: true, force: true), "holster belt worn");

            // a rifle is too large for a pack; a second sidearm fits
            var pack = entities.SpawnEntity(Backpack, map.GridCoords);
            var packed = entities.SpawnEntity(Pistol, map.GridCoords);
            Assert.That(containers.Insert(packed, entities.GetComponent<StorageComponent>(pack).Container), "pistol packed away");
            Assert.That(inventory.TryEquip(marine, pack, "back", silent: true, force: true), "backpack worn");

            arsenal.Unwind((joe, comp));
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<GunJammedComponent>(pistol), "the holstered sidearm is unwound");
                Assert.That(entities.HasComponent<GunJammedComponent>(packed), Is.False, "a gun packed in a backpack is not");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StatusPanelFollowsTheLadderAndLeavesOnStandDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            rule.ApplyLevel((joe, comp), 0);
            var status = entities.GetComponent<EWJoeStatusComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(status.Level, Is.EqualTo(0));
                Assert.That(status.LevelName, Is.EqualTo("ew-joe-level-0"));
                Assert.That(status.FinalLevel, Is.False);
            });

            rule.ApplyLevel((joe, comp), 5);
            Assert.Multiple(() =>
            {
                Assert.That(status.Level, Is.EqualTo(5));
                Assert.That(status.LevelName, Is.EqualTo("ew-joe-level-5"));
                Assert.That(status.FinalLevel, "the Melding offers nothing more");
                Assert.That(status.Objectives, Is.Empty);
            });

            entities.RemoveComponent<EWJoeComponent>(joe);
            Assert.That(entities.HasComponent<EWJoeStatusComponent>(joe), Is.False, "stand-down removes the panel");
        });

        await pair.CleanReturnAsync();
    }
}
