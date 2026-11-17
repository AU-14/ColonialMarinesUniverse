using Content.Shared._RMC14.Emplacements;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Buckle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

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

}
