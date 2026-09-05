using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class PullHandoffRegressionTest
{
    [Test]
    public async Task ChangingPullersKeepsClientJointConnectedToCurrentPuller()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        EntityUid target = default;
        var pullers = new EntityUid[3];
        NetEntity netTarget = default;
        var netPullers = new NetEntity[3];

        await server.WaitAssertion(() =>
        {
            target = server.EntMan.SpawnEntity("RMCCrateWoodenBuildable", map.GridCoords);
            netTarget = server.EntMan.GetNetEntity(target);
            for (var i = 0; i < pullers.Length; i++)
            {
                pullers[i] = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(i - 1, 0)));
                netPullers[i] = server.EntMan.GetNetEntity(pullers[i]);
            }
            server.PlayerMan.SetAttachedEntity(pair.Player!, pullers[0]);
        });
        await pair.RunUntilSynced();

        foreach (var index in new[] { 2, 1, 0, 2, 0, 1 })
        {
            await server.WaitAssertion(() =>
                Assert.That(server.EntMan.System<PullingSystem>().TryStartPull(pullers[index], target), Is.True,
                    $"Puller {index} must be able to take over the pull."));
            await pair.RunUntilSynced();
            await client.WaitAssertion(() =>
            {
                var entities = client.EntMan;
                var clientTarget = entities.GetEntity(netTarget);
                var currentPuller = entities.GetEntity(netPullers[index]);
                var pullable = entities.GetComponent<PullableComponent>(clientTarget);
                Assert.That(pullable.Puller, Is.EqualTo(currentPuller));
                var targetJoints = entities.GetComponent<JointComponent>(clientTarget).GetJoints;
                Assert.That(targetJoints, Has.Count.EqualTo(1));
                var joint = targetJoints.Values.Single();
                Assert.That(new[] { joint.BodyAUid, joint.BodyBUid }, Is.EquivalentTo(new[] { clientTarget, currentPuller }));
                var pullerJoints = entities.GetComponent<JointComponent>(currentPuller).GetJoints;
                Assert.That(pullerJoints.Values.Single(), Is.SameAs(joint));
                foreach (var other in netPullers.Where(net => net != netPullers[index]))
                {
                    if (entities.TryGetComponent<JointComponent>(entities.GetEntity(other), out var oldJoints))
                        Assert.That(oldJoints.GetJoints, Is.Empty);
                }
            });
        }
        await pair.CleanReturnAsync();
    }
}
