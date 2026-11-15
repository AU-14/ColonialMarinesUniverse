#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Vision;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Drunk;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Injuries.Pain;

[TestFixture]
public sealed class PainPlayerFeedbackTest
{
    private static readonly EntProtoId PainShockStatus = "StatusEffectCMUPainShock";
    private static readonly EntProtoId PainSuppressionStatus = "StatusEffectCMUPainSuppression";

    [Test]
    public async Task ShockFeedbackWaitsForDeadlineAndStopsWhenPainDrops()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid human = default;
        var damageAfterDue = FixedPoint2.Zero;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var feedback = entMan.GetComponent<CMUPainFeedbackComponent>(human);
            feedback.EffectInterval = TimeSpan.FromSeconds(0.25);
            feedback.NextEffect = timing.CurTime + TimeSpan.FromSeconds(0.4);
            SetPainTier(entMan, human, PainTier.Shock);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));

        await server.WaitAssertion(() =>
        {
            var damageable = server.EntMan.GetComponent<DamageableComponent>(human);
            Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Asphyxiation"), Is.EqualTo(FixedPoint2.Zero));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.3f));

        await server.WaitAssertion(() =>
        {
            var damageable = server.EntMan.GetComponent<DamageableComponent>(human);
            damageAfterDue = damageable.Damage.DamageDict.GetValueOrDefault("Asphyxiation");
            Assert.That(damageAfterDue, Is.GreaterThan(FixedPoint2.Zero));
        });

        await server.WaitPost(() => SetPainTier(server.EntMan, human, PainTier.Moderate));
        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.GetComponent<DamageableComponent>(human);
            // Respiration healing is staged off in species base.yml, so "stopped" reads as a
            // frozen value, not a decaying one. Still-running feedback would keep adding damage.
            Assert.That(
                damageable.Damage.DamageDict.GetValueOrDefault("Asphyxiation"),
                Is.EqualTo(damageAfterDue),
                "Shock feedback must stop applying asphyxiation once the pain tier drops.");
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LivingPainSuppressionRemovalRestoresShockState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        PainSuppressionComponent suppression = default!;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var painSystem = entMan.System<SharedPainShockSystem>();
            var status = entMan.System<StatusEffectsSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pain = entMan.GetComponent<PainShockComponent>(human);
            SetPainState(entMan, human, pain, PainTier.Shock);
            painSystem.AddPainSuppressionProfile(human, 0f, 4, 0f, TimeSpan.FromSeconds(10));

            Assert.That(status.TryGetStatusEffect(human, PainSuppressionStatus, out var effect), Is.True);
            suppression = entMan.GetComponent<PainSuppressionComponent>(effect!.Value);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.GetComponent<PainShockComponent>(human);
            var painSystem = entMan.System<SharedPainShockSystem>();
            var status = entMan.System<StatusEffectsSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(suppression.ActiveProfiles, Is.Not.Empty);
                Assert.That(suppression.TierSuppression, Is.EqualTo(4));
                Assert.That(painSystem.GetEffectiveTier(human, pain), Is.EqualTo(PainTier.None));
                Assert.That(status.HasStatusEffect(human, PainShockStatus), Is.False);
            });

            Assert.That(status.TryRemoveStatusEffect(human, PainSuppressionStatus), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.GetComponent<PainShockComponent>(human);
            var painSystem = entMan.System<SharedPainShockSystem>();
            var status = entMan.System<StatusEffectsSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(suppression.ActiveProfiles, Is.Empty);
                Assert.That(suppression.AccumulationSuppression, Is.Zero);
                Assert.That(suppression.TierSuppression, Is.Zero);
                Assert.That(suppression.DecayBonus, Is.Zero);
                Assert.That(painSystem.GetEffectiveTier(human, pain), Is.EqualTo(PainTier.Shock));
                Assert.That(pain.Tier, Is.EqualTo(PainTier.Shock));
                Assert.That(pain.InShock, Is.True);
                Assert.That(status.HasStatusEffect(human, PainShockStatus), Is.True);
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShockPainAppliesStrongerBreathingPressureThanSeverePain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid severe = default;
        EntityUid shock = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            severe = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            shock = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            SetPainTier(entMan, severe, PainTier.Severe);
            SetPainTier(entMan, shock, PainTier.Shock);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectsSystem>();
            var severeDamage = entMan.GetComponent<DamageableComponent>(severe).Damage.DamageDict.GetValueOrDefault("Asphyxiation");
            var shockDamage = entMan.GetComponent<DamageableComponent>(shock).Damage.DamageDict.GetValueOrDefault("Asphyxiation");
            var severeBlur = entMan.GetComponent<BlurryVisionComponent>(severe).Magnitude;
            var shockBlur = entMan.GetComponent<BlurryVisionComponent>(shock).Magnitude;

            Assert.Multiple(() =>
            {
                Assert.That(shockDamage, Is.GreaterThan(severeDamage));
                Assert.That(status.HasStatusEffect(severe, SharedDrunkSystem.Drunk), Is.False);
                Assert.That(status.TryGetTime(shock, SharedDrunkSystem.Drunk, out var shockDrunkTime), Is.True);
                Assert.That(shockDrunkTime.EndEffectTime, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(status.HasEffectComp<SlurredAccentComponent>(shock), Is.True);
                Assert.That(status.HasStatusEffect(shock, StutteringSystem.StutterEffect), Is.True);
                Assert.That(shockBlur, Is.GreaterThan(severeBlur));
                Assert.That(shockBlur, Is.LessThan(BlurryVisionComponent.MaxMagnitude),
                    "Pain shock should get bad without snapping to the full cataracts/off-white screen effect.");
            });

            entMan.DeleteEntity(severe);
            entMan.DeleteEntity(shock);
        });

        await pair.CleanReturnAsync();
    }

    private static void SetPainTier(IEntityManager entMan, EntityUid uid, PainTier tier)
    {
        var pain = entMan.GetComponent<PainShockComponent>(uid);
        SetPainState(entMan, uid, pain, tier);
        entMan.Dirty(uid, pain);
    }

    private static void SetPainState(IEntityManager entMan, EntityUid uid, PainShockComponent pain, PainTier tier)
    {
        pain.Pain = tier switch
        {
            PainTier.None => FixedPoint2.Zero,
            PainTier.Mild => PainTierThresholds.GetUpwardThreshold(PainTier.Mild),
            PainTier.Moderate => PainTierThresholds.GetUpwardThreshold(PainTier.Moderate),
            PainTier.Severe => PainTierThresholds.GetUpwardThreshold(PainTier.Severe),
            PainTier.Shock => pain.PainMax,
            _ => FixedPoint2.Zero,
        };

        pain.PainTarget = pain.Pain;
        pain.NextUpdate = TimeSpan.Zero;
        entMan.System<SharedPainShockSystem>().TickOne((uid, pain), refreshCache: false);
        entMan.Dirty(uid, pain);
    }

}

#pragma warning restore RA0002
