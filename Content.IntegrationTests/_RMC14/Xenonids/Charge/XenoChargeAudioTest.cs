#nullable enable

using System.Linq;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Server.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Xenonids.Charge;

[TestFixture, TestOf(typeof(XenoChargeSystem))]
public sealed class XenoChargeAudioTest
{
    [Test]
    public async Task CrusherChargePlaysConfiguredWindupSound()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var listener = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var crusher = entMan.SpawnEntity("RMCXenoCrusher", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(session, listener), Is.True);

            var actions = server.System<SharedActionsSystem>();
            var worldTargetQuery = entMan.GetEntityQuery<WorldTargetActionComponent>();
            var chargeAction = actions.GetActions(crusher).Single(action =>
                worldTargetQuery.CompOrNull(action)?.Event is XenoChargeActionEvent);
            var chargeEvent = new XenoChargeActionEvent
            {
                Target = map.GridCoords,
            };

            actions.PerformAction(crusher, chargeAction, chargeEvent);

            Assert.Multiple(() =>
            {
                Assert.That(chargeEvent.Handled, Is.True);
                Assert.That(
                    HasAudioFile(entMan, "/Audio/_RMC14/Xeno/crusher_windup_sound.ogg"),
                    Is.True,
                    "Starting a crusher charge should play its configured windup sound.");
            });

            entMan.DeleteEntity(crusher);
            entMan.DeleteEntity(listener);
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasAudioFile(IEntityManager entMan, string fileName)
    {
        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out _, out var audio))
        {
            if (audio.FileName == fileName)
                return true;
        }

        return false;
    }
}
