using Content.Shared._RMC14.Stun;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Map;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests._RMC14.Stun;

[TestFixture]
public sealed class RMCUnconsciousMuteTest
{
    private const string IndependentMute = "StatusEffectMuted";
    private const string KnockoutMute = "StatusEffectRMCUnconsciousMuted";
    private const string Unconscious = "Unconscious";

    [Test]
    public async Task KnockoutMuteRefreshAndRemovalPreserveIndependentMute()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid target = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var knockout = entities.System<RMCSizeStunSystem>();
            var statuses = entities.System<NewStatusEffectsSystem>();
            target = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            Assert.That(statuses.TrySetStatusEffectDuration(
                target,
                IndependentMute,
                TimeSpan.FromMinutes(1)), Is.True);
            Assert.That(knockout.TryKnockOut(target, TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(statuses.TryGetStatusEffect(target, KnockoutMute, out var firstOwnedMute), Is.True);

            var firstEnd = entities.GetComponent<StatusEffectComponent>(firstOwnedMute!.Value).EndEffectTime;
            Assert.Multiple(() =>
            {
                Assert.That(statuses.HasStatusEffect(target, IndependentMute), Is.True);
                Assert.That(statuses.HasStatusEffect(target, KnockoutMute), Is.True);
                Assert.That(entities.HasComponent<MutedStatusEffectComponent>(firstOwnedMute.Value), Is.True);
            });

            Assert.That(knockout.TryKnockOut(target, TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(statuses.TryGetStatusEffect(target, KnockoutMute, out var refreshedOwnedMute), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(refreshedOwnedMute, Is.EqualTo(firstOwnedMute),
                    "Refreshing knockout created a duplicate owned mute entity.");
                Assert.That(entities.GetComponent<StatusEffectComponent>(refreshedOwnedMute!.Value).EndEffectTime,
                    Is.GreaterThan(firstEnd));
            });
        });

        await pair.RunTicksSync(1);

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var legacyStatuses = entities.System<StatusEffectQuerySystem>();
            Assert.That(legacyStatuses.TryRemoveStatusEffect(target, Unconscious), Is.True);
        });

        await pair.RunTicksSync(1);

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var statuses = entities.System<NewStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(statuses.HasStatusEffect(target, KnockoutMute), Is.False,
                    "Ending knockout left its owned mute active.");
                Assert.That(statuses.HasStatusEffect(target, IndependentMute), Is.True,
                    "Ending knockout removed an independent mute source.");
            });

            entities.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }
}
