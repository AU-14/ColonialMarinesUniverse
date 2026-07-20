using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Lock;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Interaction;

[TestFixture]
[TestOf(typeof(LockSystem))]
public sealed class LockActivationHandledTest
{
    private const string LockPrototype = "LockActivationHandledTestTarget";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: LockActivationHandledTestTarget
  components:
  - type: Lock
    locked: true
    unlockOnClick: true
  - type: AccessReader
    access: [[""Captain""]]
";

    [Test]
    public async Task FailedUnlockStillConsumesActivation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var user = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<ComplexInteractionComponent>(user);
            var target = entMan.SpawnEntity(LockPrototype, MapCoordinates.Nullspace);
            var lockComponent = entMan.GetComponent<LockComponent>(target);
            var activate = new ActivateInWorldEvent(user, target, complex: true);

            entMan.EventBus.RaiseLocalEvent(target, activate);

            Assert.Multiple(() =>
            {
                Assert.That(activate.Handled, Is.True);
                Assert.That(lockComponent.Locked, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
