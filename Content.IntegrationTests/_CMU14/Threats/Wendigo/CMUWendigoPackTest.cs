using Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoPackTest
{
    private const string Lab = "CMUWendigoLab";
    private const string Human = "CMMobHuman";

    /// <summary>
    /// Lab Wendigos are allies of each other but not of humans, cannot maul each other, share the pack
    /// band and announce a packmate's death without throwing.
    /// </summary>
    [Test]
    public async Task PackMembersAreAlliesShareRadioAndSurviveDeath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pack = entMan.System<SharedCMUWendigoPackSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var first = entMan.SpawnEntity(Lab, map.GridCoords);
            var second = entMan.SpawnEntity(Lab, map.GridCoords);
            var human = entMan.SpawnEntity(Human, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(pack.IsAlly(first, second), Is.True);
                Assert.That(pack.IsAlly(second, first), Is.True);
                Assert.That(pack.IsAlly(first, human), Is.False);
                Assert.That(pack.IsAlly(human, second), Is.False);
            });

            var attack = new AttackAttemptEvent(first, second);
            entMan.EventBus.RaiseLocalEvent(first, attack);
            Assert.That(attack.Cancelled, Is.True, "A pack member must not be able to attack a packmate.");

            Assert.Multiple(() =>
            {
                Assert.That(HasPackChannel(entMan, first), Is.True);
                Assert.That(HasPackChannel(entMan, second), Is.True);
                Assert.That(HasPackChannel(entMan, human), Is.False);
            });

            // The death announcement goes to the living pack; it must not throw.
            Assert.DoesNotThrow(() => mobState.ChangeMobState(first, MobState.Dead));
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasPackChannel(IEntityManager entMan, EntityUid uid)
    {
        return entMan.TryGetComponent(uid, out ActiveRadioComponent? radio) &&
               radio.Channels.Contains(CMUWendigoPackComponent.PackChannel);
    }
}
