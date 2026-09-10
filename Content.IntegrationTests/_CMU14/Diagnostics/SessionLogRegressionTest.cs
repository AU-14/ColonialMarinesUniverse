using System.Linq;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Sensor;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Storage;
using Content.Shared.Trigger;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class SessionLogRegressionTest
{
    [Test]
    public async Task SuccessiveMountOperatorsCanUseDismountAction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var actions = entities.System<SharedActionsSystem>();
            var mount = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var mountComponent = entities.AddComponent<WeaponMountComponent>(mount);
            var strap = entities.AddComponent<StrapComponent>(mount);
            var weapon = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            typeof(WeaponMountComponent).GetField(nameof(mountComponent.MountedEntity))!.SetValue(mountComponent, weapon);
            EntityUid? sharedAction = null;

            for (var i = 0; i < 2; i++)
            {
                var user = entities.SpawnEntity(null, MapCoordinates.Nullspace);
                var buckle = entities.AddComponent<BuckleComponent>(user);
                typeof(BuckleComponent).GetField(nameof(buckle.BuckledTo))!.SetValue(buckle, mount);
                var strapped = new StrappedEvent((mount, strap), (user, buckle));
                entities.EventBus.RaiseLocalEvent(mount, ref strapped);

                var actionId = mountComponent.DismountActionEntity!.Value;
                var action = entities.GetComponent<ActionComponent>(actionId);
                sharedAction ??= actionId;
                Assert.That(actionId, Is.EqualTo(sharedAction));
                Assert.That(action.Container, Is.EqualTo(mount));
                Assert.That(action.AttachedEntity, Is.EqualTo(user));
                actions.PerformAction(user, (actionId, action));
                Assert.That(buckle.Buckled, Is.False, "The action must be raised on the current operator.");
                Assert.That(entities.HasComponent<WeaponControllerComponent>(user), Is.False);
                Assert.That(action.AttachedEntity, Is.Null);
                entities.DeleteEntity(user);
            }
            entities.DeleteEntity(mount);
            entities.DeleteEntity(weapon);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareIgnoresDeletedTriggerUser()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var snare = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<SapperSnareComponent>(snare);
            var user = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.DeleteEntity(user);
            var ev = new TriggerEvent(user);
            Assert.DoesNotThrow(() => entities.EventBus.RaiseLocalEvent(snare, ref ev));
            Assert.That(entities.EntityExists(snare), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(SensorTowerState.On)]
    [TestCase(SensorTowerState.Off)]
    public async Task RepairedSensorTowerIgnoresOrdinaryTools(SensorTowerState state)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var tower = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var component = entities.AddComponent<SensorTowerComponent>(tower);
            typeof(SensorTowerComponent).GetField(nameof(component.State))!.SetValue(component, state);
            typeof(SensorTowerComponent).GetField(nameof(component.SkillLevel))!.SetValue(component, 0);
            var user = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var tool = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var ev = new InteractUsingEvent(user, tool, tower, EntityCoordinates.Invalid);
            Assert.DoesNotThrow(() => entities.EventBus.RaiseLocalEvent(tower, ev));
            Assert.That(component.State, Is.EqualTo(state));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CustomStampLabelsAndStockLocalizationBothDisplay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var label in new[] { "like pizza", "like crackers", "HUMINT CLASSIFIED" })
                Assert.That(new StampDisplayInfo { StampedName = label }.GetDisplayName(), Is.EqualTo(label));
            const string stock = "stamp-component-stamped-name-default";
            Assert.That(new StampDisplayInfo { StampedName = stock }.GetDisplayName(), Is.EqualTo(Loc.GetString(stock)));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GromMachinegunnerBeltFitsAllStartingItems()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            EntProtoId prototype = "AU14BeltSmartGunOperatorPistolUPPFull";
            var belt = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var items = entities.GetComponent<StorageComponent>(belt).Container.ContainedEntities;
            Assert.That(items, Has.Count.EqualTo(5));
            Assert.That(items.Count(item => entities.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == "RMCMagazineLMGQYJ72"), Is.EqualTo(2));
            entities.DeleteEntity(belt);
        });
        await pair.CleanReturnAsync();
    }
}
