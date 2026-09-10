using System.Linq;
using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Overlays;
using Content.Shared.Radio;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaSmokeTest
{
    private static readonly ProtoId<RadioChannelPrototype> YautjaRadioChannel = "CMUYautja";

    private static readonly string[] VoiceActionIds =
    {
        "CMUActionYautjaVoiceClick",
        "CMUActionYautjaVoiceRoar",
        "CMUActionYautjaVoiceLaugh",
        "CMUActionYautjaVoiceGrowl",
        "CMUActionYautjaVoicePain",
        "CMUActionYautjaVoiceDistract",
        "CMUActionYautjaVoiceDeathCry",
        "CMUActionYautjaVoiceDeathLaugh",
    };

    private static readonly string[] ClanArmorLoadoutIds =
    {
        "CMUYautjaClanArmor",
        "CMUYautjaClanArmorBronze",
        "CMUYautjaClanArmorSilver",
        "CMUYautjaClanArmorCrimson",
        "CMUYautjaClanArmorBone",
    };

    [Test]
    public async Task BiomaskDoesNotContainMotionDetector()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryGetSlotEntity(hunter, "mask", out var mask), Is.True);
                Assert.That(mask, Is.Not.Null);
                Assert.That(entMan.HasComponent<MotionDetectorComponent>(mask), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BiomaskVisorAppliesThermalOverlay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);

            try
            {
                var vision = entMan.GetComponent<NightVisionComponent>(hunter);
                Assert.Multiple(() =>
                {
                    Assert.That(vision.State, Is.EqualTo(NightVisionState.Full));
                    Assert.That(vision.Overlay, Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BiomaskProvidesIntegratedHealthHudWithoutJobIcons()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var xeno = entMan.SpawnEntity("CMXenoWarrior", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryGetSlotEntity(hunter, "mask", out var mask), Is.True);
                Assert.That(mask, Is.Not.Null);
                Assert.That(entMan.HasComponent<ShowJobIconsComponent>(mask), Is.False);
                Assert.That(entMan.HasComponent<HolocardScannerComponent>(mask), Is.True);

                var bars = entMan.GetComponent<ShowHealthBarsComponent>(mask!.Value);
                var icons = entMan.GetComponent<ShowHealthIconsComponent>(mask.Value);
                var xenoContainer = entMan.GetComponent<Content.Shared.Damage.Components.InjurableComponent>(xeno)
                    .DamageContainer;
                var expectedContainers = new[] { "Biological", "BiologicalMetaphysical", "Inorganic", "Silicon", "Xeno" };
                Assert.Multiple(() =>
                {
                    Assert.That(xenoContainer?.Id, Is.EqualTo("Xeno"));
                    Assert.That(bars.DamageContainers.Select(id => id.Id), Is.EquivalentTo(expectedContainers));
                    Assert.That(icons.DamageContainers.Select(id => id.Id), Is.EquivalentTo(expectedContainers));
                });
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectYautjaSpawnGetsCoreLoadout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                AssertEquipped(entMan, inventory, hunter, "mask", "CMUYautjaMask");
                AssertEquipped(entMan, inventory, hunter, "gloves", "CMUYautjaBracer");
                AssertEquipped(entMan, inventory, hunter, "back", "CMUYautjaCloakPack");
                AssertEquippedAny(entMan, inventory, hunter, "outerClothing", ClanArmorLoadoutIds);
                AssertEquipped(entMan, inventory, hunter, "jumpsuit", "CMUYautjaBodyMesh");
                AssertEquipped(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreaves");
                AssertEquipped(entMan, inventory, hunter, "pocket1", "CMUYautjaSmartDisc");
                AssertEquipped(entMan, inventory, hunter, "pocket2", "CMUYautjaMedicomp");

                var movement = entMan.GetComponent<MovementSpeedModifierComponent>(hunter);
                Assert.That(movement.BaseWalkSpeed, Is.EqualTo(3.7f));
                Assert.That(movement.BaseSprintSpeed, Is.EqualTo(7.1f));
                Assert.That(server.ProtoMan.Index(YautjaRadioChannel).KeyCode, Is.EqualTo('9'));

                foreach (var action in VoiceActionIds)
                    Assert.That(HasAction(entMan, hunter, action), Is.False, action);

                Assert.That(CountActions(entMan, hunter, "ActionCombatModeToggle"), Is.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RandomYautjaSpawnHasOneCombatModeAndNoVoiceActions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var randomHumanoid = entMan.System<RandomHumanoidSystem>();
            var hunter = randomHumanoid.SpawnRandomHumanoid("CMUYautjaHunter", EntityCoordinates.Invalid, string.Empty);

            try
            {
                foreach (var action in VoiceActionIds)
                    Assert.That(HasAction(entMan, hunter, action), Is.False, action);

                Assert.That(CountActions(entMan, hunter, "ActionCombatModeToggle"), Is.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerFabricatesRationAndCanteen()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var rationAction = entMan.SpawnEntity("CMUActionYautjaCreateFieldRation", MapCoordinates.Nullspace);
            var canteenAction = entMan.SpawnEntity("CMUActionYautjaCreateHuntingCanteen", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var rationActionComp = entMan.GetComponent<ActionComponent>(rationAction);
                var rationEvent = new YautjaCreateFieldRationActionEvent
                {
                    Performer = hunter,
                    Action = (rationAction, rationActionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, rationEvent);

                var ration = hands.GetActiveItem(hunter);
                Assert.That(ration, Is.Not.Null);
                Assert.That(entMan.GetComponent<MetaDataComponent>(ration!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaFieldRation"));
                entMan.DeleteEntity(ration.Value);

                var canteenActionComp = entMan.GetComponent<ActionComponent>(canteenAction);
                var canteenEvent = new YautjaCreateHuntingCanteenActionEvent
                {
                    Performer = hunter,
                    Action = (canteenAction, canteenActionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, canteenEvent);

                var canteen = hands.GetActiveItem(hunter);
                Assert.That(canteen, Is.Not.Null);
                Assert.That(entMan.GetComponent<MetaDataComponent>(canteen!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaHuntingCanteen"));
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(rationAction);
                entMan.DeleteEntity(canteenAction);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerStoredGearDeploysAndRetractsSameEntity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Scimitar, out var scimitar), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(scimitar), Is.False);

                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(scimitar), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicompSpawnsReferenceHealingSet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var medicomp = entMan.SpawnEntity("CMUYautjaMedicomp", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(medicomp);
                var prototypes = storage.Container.ContainedEntities
                    .Select(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID)
                    .ToList();

                Assert.That(prototypes, Does.Contain("CMUYautjaHealingGun"));
                Assert.That(prototypes.Count(id => id == "CMUYautjaWoundClamp"), Is.EqualTo(2));
                Assert.That(prototypes.Count(id => id == "CMUYautjaHealthShard"), Is.EqualTo(6));
                Assert.That(prototypes.Count(id => id == "CMUYautjaStabilisingCrystal"), Is.EqualTo(2));
                Assert.That(prototypes, Does.Contain("CMUYautjaAlienHealthAnalyzer"));
                Assert.That(prototypes.Count(id => id == "CMUYautjaHerbalCase"), Is.EqualTo(2));

                foreach (var herbalCase in storage.Container.ContainedEntities
                             .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaHerbalCase"))
                {
                    var herbalStorage = entMan.GetComponent<StorageComponent>(herbalCase);
                    var bruisePackTotal = herbalStorage.Container.ContainedEntities
                        .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaAdvancedBruisePack")
                        .Sum(pack => entMan.GetComponent<StackComponent>(pack).Count);
                    var ointmentTotal = herbalStorage.Container.ContainedEntities
                        .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaAdvancedOintment")
                        .Sum(ointment => entMan.GetComponent<StackComponent>(ointment).Count);

                    Assert.That(bruisePackTotal, Is.EqualTo(4));
                    Assert.That(ointmentTotal, Is.EqualTo(4));
                }

                var healingGelTotal = storage.Container.ContainedEntities
                    .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaHealingGel")
                    .Sum(gel => entMan.GetComponent<StackComponent>(gel).Count);
                Assert.That(healingGelTotal, Is.EqualTo(12));

                var stabilizerGelTotal = storage.Container.ContainedEntities
                    .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaStabilizerGel")
                    .Sum(gel => entMan.GetComponent<StackComponent>(gel).Count);
                Assert.That(stabilizerGelTotal, Is.EqualTo(3));
            }
            finally
            {
                if (!entMan.Deleted(medicomp))
                    entMan.DeleteEntity(medicomp);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructCannotBeArmedWhileCritical()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                mobState.ChangeMobState(hunter, MobState.Critical);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertEquipped(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        string prototype)
    {
        AssertEquippedAny(entMan, inventory, wearer, slot, prototype);
    }

    private static void AssertEquippedAny(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        params string[] prototypes)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, slot);
        Assert.That(equipped, Is.Not.Null, slot);

        var meta = entMan.GetComponent<MetaDataComponent>(equipped.Value);
        Assert.That(prototypes, Does.Contain(meta.EntityPrototype?.ID), slot);
    }

    private static YautjaToggleScimitarActionEvent NewToggleScimitarEvent(
        EntityUid hunter,
        EntityUid action,
        ActionComponent actionComp)
    {
        return new YautjaToggleScimitarActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
        };
    }

    private static bool HasAction(IEntityManager entMan, EntityUid user, string prototype)
    {
        if (!entMan.TryGetComponent<ActionsComponent>(user, out var actions))
            return false;

        foreach (var action in actions.Actions)
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }

    private static int CountActions(IEntityManager entMan, EntityUid user, string prototype)
    {
        if (!entMan.TryGetComponent<ActionsComponent>(user, out var actions))
            return 0;

        return actions.Actions.Count(action =>
            entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == prototype);
    }
}
