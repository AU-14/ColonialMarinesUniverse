using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared._RMC14.Weapons.Ranged.Ammo;
using Content.Shared._RMC14.Weapons.Ranged.Auto;
using Content.Shared._RMC14.Weapons.Ranged.Recoil;
using Content.Shared.Actions;
using Serilog.Events;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class SmartGunReconnectActionTest : GameTest
{
    [Test]
    public async Task PendingClientContainerStateKeepsReplicatedSmartGunActions()
    {
        var map = await Pair.CreateTestMap();
        EntityUid smartGun = default;

        await Server.WaitAssertion(() =>
        {
            smartGun = SEntMan.SpawnEntity("RMCSmartGunPMC", map.GridCoords);
            var getActions = new GetItemActionsEvent(Server.System<ActionContainerSystem>(), smartGun, smartGun);
            SEntMan.EventBus.RaiseLocalEvent(smartGun, getActions);
        });
        await Pair.RunUntilSynced();

        var invalidActionContainers = 0;

        bool JudgeInvalidActionContainer(string sawmill, LogEvent message)
        {
            if (sawmill != "system.action_container" ||
                message.Level != LogEventLevel.Error ||
                !message.RenderMessage().Contains("is not contained in the expected container", StringComparison.Ordinal))
            {
                return false;
            }

            invalidActionContainers++;
            return true;
        }

        Pair.ClientLogHandler.JudgeLog += JudgeInvalidActionContainer;
        try
        {
            await Client.WaitAssertion(() =>
            {
                var clientGun = ToClientUid(smartGun);
                var actions = GetActionReferences(clientGun);
                var transform = Client.System<SharedTransformSystem>();

                foreach (var action in actions)
                {
                    transform.DetachEntity(action, CEntMan.GetComponent<TransformComponent>(action));
                }

                var getActions = new GetItemActionsEvent(Client.System<ActionContainerSystem>(), clientGun, clientGun);
                CEntMan.EventBus.RaiseLocalEvent(clientGun, getActions);

                Assert.That(GetActionReferences(clientGun), Is.EqualTo(actions));
            });
        }
        finally
        {
            Pair.ClientLogHandler.JudgeLog -= JudgeInvalidActionContainer;
        }

        Assert.That(invalidActionContainers, Is.Zero);
    }

    private EntityUid[] GetActionReferences(EntityUid smartGun)
    {
        return
        [
            CEntMan.GetComponent<GunToggleableRecoilComponent>(smartGun).Action!.Value,
            CEntMan.GetComponent<GunToggleableAutoFireComponent>(smartGun).Action!.Value,
            CEntMan.GetComponent<GunToggleableAmmoComponent>(smartGun).Action!.Value,
            CEntMan.GetComponent<ToggleableMotionDetectorComponent>(smartGun).Action!.Value,
        ];
    }
}
