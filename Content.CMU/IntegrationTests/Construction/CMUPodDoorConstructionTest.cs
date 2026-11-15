using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Doors;
using Content.Shared.Construction;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Construction;

[TestFixture]
public sealed class CMUPodDoorConstructionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("CMUPodDoorBuild", "CMUConstructedPodDoor")]
    [TestCase("CMUShutterBuild", "CMUConstructedShutter")]
    [TestCase("CMUPodDoorButtonBuild", "CMUConstructedPodDoorButton")]
    public async Task EngineeringSkillRequiredAndBuilderRecorded(string recipe, string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var metal = SEntMan.SpawnEntity("CMSheetMetal", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, metal), Is.True);
            var skills = Server.System<SkillsSystem>();
            skills.SetSkill(user, "RMCSkillEngineer", 0);
            var construction = Server.System<RMCConstructionSystem>();
            var item = SEntMan.GetComponent<RMCConstructionItemComponent>(metal);
            Assert.That(item.Buildable!.Any(p => p.ToString() == recipe), Is.True);
            Assert.That(construction.Build((metal, item), user, recipe, 1), Is.False);
            skills.SetSkill(user, "RMCSkillEngineer", 1);
            Assert.That(construction.Build((metal, item), user, recipe, 1), Is.True);
        });
        await Pair.RunSeconds(22);
        await Server.WaitAssertion(() =>
        {
            var built = SEntMan.EntityQuery<CMUConstructedDoorControlComponent>()
                .Single(c => SEntMan.GetComponent<MetaDataComponent>(c.Owner).EntityPrototype?.ID == prototype);
            Assert.That(built.Builder, Is.EqualTo(user));
        });
    }
}

[TestFixture]
public sealed class CMUPodDoorNetworkTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task PredictedButtonDoesNotAffectMappedDoorsAndServerStateReplicates()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid button = default;
        EntityUid door = default;
        NetEntity userNet = default;
        NetEntity buttonNet = default;
        NetEntity doorNet = default;
        NetEntity mappedNet = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            button = SEntMan.SpawnEntity("CMUConstructedPodDoorButton", map.GridCoords);
            door = SEntMan.SpawnEntity("CMUConstructedPodDoor", map.GridCoords.Offset(new Vector2(1, 0)));
            var mapped = SEntMan.SpawnEntity("RMCPodDoor", map.GridCoords.Offset(new Vector2(2, 0)));
            Server.System<MetaDataSystem>().SetEntityName(mapped, "CMUConstructedControl");
            var buttonBuilt = new ConstructionCompletedEvent(button, user);
            SEntMan.EventBus.RaiseLocalEvent(button, ref buttonBuilt, true);
            var doorBuilt = new ConstructionCompletedEvent(door, user);
            SEntMan.EventBus.RaiseLocalEvent(door, ref doorBuilt, true);
            var tool = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(button, new InteractUsingEvent(user, tool, button, map.GridCoords));
            SEntMan.EventBus.RaiseLocalEvent(door, new InteractUsingEvent(user, tool, door, map.GridCoords));
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(button, false);
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(door, false);
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(mapped, false);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            userNet = SEntMan.GetNetEntity(user);
            buttonNet = SEntMan.GetNetEntity(button);
            doorNet = SEntMan.GetNetEntity(door);
            mappedNet = SEntMan.GetNetEntity(mapped);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientButton = CEntMan.GetEntity(buttonNet);
            var clientUser = CEntMan.GetEntity(userNet);
            Assert.That(CEntMan.HasComponent<CMUConstructedDoorControlComponent>(clientButton), Is.True);
            var buttonControl = CEntMan.GetComponent<CMUConstructedDoorControlComponent>(clientButton);
            Assert.That(buttonControl.Builder, Is.EqualTo(clientUser));
            Assert.That(buttonControl.LinkedDoors, Does.Contain(CEntMan.GetEntity(doorNet)));
            CEntMan.EventBus.RaiseLocalEvent(clientButton, new ActivateInWorldEvent(clientUser, clientButton, true));
            Assert.That(CEntMan.GetComponent<DoorComponent>(CEntMan.GetEntity(mappedNet)).State, Is.EqualTo(DoorState.Closed));
        });
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(button, new ActivateInWorldEvent(user, button, true)));
        await Pair.RunSeconds(2);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<DoorComponent>(CEntMan.GetEntity(doorNet)).State, Is.EqualTo(DoorState.Open));
            Assert.That(CEntMan.GetComponent<DoorComponent>(CEntMan.GetEntity(mappedNet)).State, Is.EqualTo(DoorState.Closed));
        });
    }
}

[TestFixture]
public sealed class CMUPodDoorLinkingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LinksAreExplicitAndBuilderCanProtectBothEndpoints()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var builder = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var other = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var tool = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            var button = SEntMan.SpawnEntity("CMUConstructedPodDoorButton", map.GridCoords);
            var door = SEntMan.SpawnEntity("CMUConstructedPodDoor", map.GridCoords.Offset(new Vector2(1, 0)));
            var shutter = SEntMan.SpawnEntity("CMUConstructedShutter", map.GridCoords.Offset(new Vector2(2, 0)));
            foreach (var target in new[] { button, door, shutter })
            {
                var built = new ConstructionCompletedEvent(target, builder);
                SEntMan.EventBus.RaiseLocalEvent(target, ref built, true);
            }

            var control = SEntMan.GetComponent<CMUConstructedDoorControlComponent>(button);
            void Use(EntityUid user, EntityUid target) =>
                SEntMan.EventBus.RaiseLocalEvent(target, new InteractUsingEvent(user, tool, target, map.GridCoords));
            void Toggle(EntityUid user, EntityUid target)
            {
                var verbs = new GetVerbsEvent<Verb>(user, target, null, null, true, true, true, []);
                SEntMan.EventBus.RaiseLocalEvent(target, verbs);
                verbs.Verbs.Single(v => v.Text is "Lock links" or "Unlock links").Act!();
            }

            Toggle(builder, door);
            Use(other, button);
            Use(other, door);
            Assert.That(control.LinkedDoors, Is.Empty, "A protected door cannot be linked by another player.");
            Use(builder, button);
            Use(builder, door);
            Assert.That(control.LinkedDoors, Is.EquivalentTo(new[] { door }));
            Use(other, shutter);
            Assert.That(control.LinkedDoors, Does.Not.Contain(shutter), "A different player cannot reuse the multitool selection.");
            Use(other, button);
            Use(other, door);
            Assert.That(control.LinkedDoors, Does.Contain(door), "A protected door cannot be unlinked by another player.");
            Use(other, shutter);
            Assert.That(control.LinkedDoors, Does.Contain(shutter), "Unprotected links can be changed.");
            Toggle(builder, button);
            Use(other, button);
            Use(other, shutter);
            Assert.That(control.LinkedDoors, Does.Contain(shutter), "Protecting the button also blocks changes from an existing selection.");
            Use(builder, button);
            Use(builder, door);
            Assert.That(control.LinkedDoors, Does.Not.Contain(door), "The builder can change protected links.");
            SEntMan.DeleteEntity(shutter);
            Assert.That(control.LinkedDoors, Is.Empty, "Destroyed doors are removed from controls.");
        });
    }

    [Test]
    public async Task ControlsCannotLinkAcrossMaps()
    {
        var map = await Pair.CreateTestMap();
        var otherMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var tool = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            var button = SEntMan.SpawnEntity("CMUConstructedPodDoorButton", map.GridCoords);
            var door = SEntMan.SpawnEntity("CMUConstructedPodDoor", otherMap.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(button, new InteractUsingEvent(user, tool, button, map.GridCoords));
            SEntMan.EventBus.RaiseLocalEvent(door, new InteractUsingEvent(user, tool, door, otherMap.GridCoords));
            Assert.That(SEntMan.GetComponent<CMUConstructedDoorControlComponent>(button).LinkedDoors, Is.Empty);
        });
    }

    [Test]
    public async Task ConstructedButtonOnlyControlsExplicitlyLinkedDoors()
    {
        var map = await Pair.CreateTestMap();
        EntityUid button = default;
        EntityUid user = default;
        EntityUid linked = default;
        EntityUid unlinked = default;
        EntityUid mapped = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            button = SEntMan.SpawnEntity("CMUConstructedPodDoorButton", map.GridCoords);
            linked = SEntMan.SpawnEntity("CMUConstructedPodDoor", map.GridCoords.Offset(new Vector2(1, 0)));
            unlinked = SEntMan.SpawnEntity("CMUConstructedPodDoor", map.GridCoords.Offset(new Vector2(2, 0)));
            mapped = SEntMan.SpawnEntity("RMCPodDoor", map.GridCoords.Offset(new Vector2(3, 0)));
            var tool = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            var builder = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var built = new ConstructionCompletedEvent(button, builder);
            SEntMan.EventBus.RaiseLocalEvent(button, ref built, true);
            SEntMan.EventBus.RaiseLocalEvent(button, new InteractUsingEvent(user, tool, button, map.GridCoords));
            SEntMan.EventBus.RaiseLocalEvent(linked, new InteractUsingEvent(user, tool, linked, map.GridCoords));
            var verbs = new GetVerbsEvent<Verb>(builder, button, null, null, true, true, true, []);
            SEntMan.EventBus.RaiseLocalEvent(button, verbs);
            verbs.Verbs.Single(v => v.Text == "Lock links").Act!();
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(button, false);
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(linked, false);
            SEntMan.EventBus.RaiseLocalEvent(button, new ActivateInWorldEvent(user, button, true));
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DoorComponent>(linked).State, Is.EqualTo(DoorState.Open));
            Assert.That(SEntMan.GetComponent<DoorComponent>(unlinked).State, Is.EqualTo(DoorState.Closed));
            Assert.That(SEntMan.GetComponent<DoorComponent>(mapped).State, Is.EqualTo(DoorState.Closed));
        });
    }

    [Test]
    public async Task MappedButtonStillUsesNamesButIgnoresConstructedDoors()
    {
        var map = await Pair.CreateTestMap();
        EntityUid mapped = default;
        EntityUid constructed = default;
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var button = SEntMan.SpawnEntity("RMCPodDoorButton", map.GridCoords);
            Server.System<MetaDataSystem>().SetEntityName(button, "podlock");
            mapped = SEntMan.SpawnEntity("RMCPodDoor", map.GridCoords.Offset(new Vector2(1, 0)));
            constructed = SEntMan.SpawnEntity("CMUConstructedPodDoor", map.GridCoords.Offset(new Vector2(2, 0)));
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(button, false);
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(mapped, false);
            Server.System<SharedPowerReceiverSystem>().SetNeedsPower(constructed, false);
            SEntMan.EventBus.RaiseLocalEvent(button, new ActivateInWorldEvent(user, button, true));
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DoorComponent>(mapped).State, Is.EqualTo(DoorState.Open));
            Assert.That(SEntMan.GetComponent<DoorComponent>(constructed).State, Is.EqualTo(DoorState.Closed));
        });
    }
}
