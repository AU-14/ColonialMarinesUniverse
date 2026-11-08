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

}
