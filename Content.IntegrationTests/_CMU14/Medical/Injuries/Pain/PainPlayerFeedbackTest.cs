using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Injuries.Vision;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Medical.Injuries.Pain;

[TestFixture]
public sealed class PainPlayerFeedbackTest
{
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
            Assert.That(damageable.Damage.DamageDict["Asphyxiation"], Is.EqualTo(FixedPoint2.Zero));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.3f));

        await server.WaitAssertion(() =>
        {
            var damageable = server.EntMan.GetComponent<DamageableComponent>(human);
            damageAfterDue = damageable.Damage.DamageDict["Asphyxiation"];
            Assert.That(damageAfterDue, Is.GreaterThan(FixedPoint2.Zero));
        });

        await server.WaitPost(() => SetPainTier(server.EntMan, human, PainTier.Moderate));
        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.GetComponent<DamageableComponent>(human);
            Assert.That(
                damageable.Damage.DamageDict["Asphyxiation"],
                Is.LessThan(damageAfterDue),
                "Natural respiration should reduce asphyxiation when no new shock feedback is scheduled.");
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeverePainAppliesBlurOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            SetPainTier(entMan, human, PainTier.Severe);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var oldStatus = entMan.System<StatusEffectQuerySystem>();
            var feedback = entMan.GetComponent<CMUPainFeedbackComponent>(human);
            var damageable = entMan.GetComponent<DamageableComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(oldStatus.HasStatusEffect(human, "Stutter"), Is.False);
                Assert.That(oldStatus.HasStatusEffect(human, "Drunk"), Is.False);
                Assert.That(oldStatus.HasStatusEffect(human, "SlurredSpeech"), Is.False);
                Assert.That(entMan.HasComponent<BlurryVisionComponent>(human), Is.True);
                Assert.That(entMan.GetComponent<BlurryVisionComponent>(human).Magnitude,
                    Is.EqualTo(feedback.SevereBlurStartAmount));
                Assert.That(feedback.SevereBlurStartAmount, Is.LessThan(0.5f));
                Assert.That(damageable.Damage.DamageDict["Asphyxiation"], Is.EqualTo(FixedPoint2.Zero));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TemporaryPainBlurExpiresWhenNotRefreshed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.System<CMUTemporaryBlurryVisionSystem>()
                .AddTemporaryBlurModifier(human, TimeSpan.FromSeconds(1), 2);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<BlurryVisionComponent>(human).Magnitude, Is.EqualTo(2));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BlurryVisionComponent>(human), Is.False);
            server.EntMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TemporaryPainBlurBypassesEyeDamageDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.System<BlindableSystem>().AdjustEyeDamage(human, 5);
            entMan.System<CMUTemporaryBlurryVisionSystem>()
                .AddTemporaryBlurModifier(human, TimeSpan.FromSeconds(10), 2);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<BlurryVisionComponent>(human).Magnitude, Is.EqualTo(2));
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ModeratePainDoesNotApplyPlayerFacingFeedback()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            SetPainTier(entMan, human, PainTier.Moderate);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var oldStatus = entMan.System<StatusEffectQuerySystem>();
            var damageable = entMan.GetComponent<DamageableComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(oldStatus.HasStatusEffect(human, "Stutter"), Is.False);
                Assert.That(oldStatus.HasStatusEffect(human, "Drunk"), Is.False);
                Assert.That(oldStatus.HasStatusEffect(human, "SlurredSpeech"), Is.False);
                Assert.That(entMan.HasComponent<BlurryVisionComponent>(human), Is.False);
                Assert.That(damageable.Damage.DamageDict["Asphyxiation"], Is.EqualTo(FixedPoint2.Zero));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PainBlurInterpolatesAfterSevereThreshold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            SetPainValue(entMan, human, (FixedPoint2)84);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var feedback = entMan.GetComponent<CMUPainFeedbackComponent>(human);
            var blur = entMan.GetComponent<BlurryVisionComponent>(human).Magnitude;
            var expected = GetExpectedPainBlur(feedback, feedback.SevereBlurEquivalentPain);

            Assert.Multiple(() =>
            {
                Assert.That(blur, Is.EqualTo(expected).Within(0.01f));
                Assert.That(blur, Is.LessThan(0.5f));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PainBlurInterpolatesAfterShockThreshold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            SetPainValue(entMan, human, (FixedPoint2)90);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var feedback = entMan.GetComponent<CMUPainFeedbackComponent>(human);
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var blur = entMan.GetComponent<BlurryVisionComponent>(human).Magnitude;
            var expected = GetExpectedPainBlur(feedback, feedback.ShockBlurEquivalentPain);

            Assert.Multiple(() =>
            {
                Assert.That(blur, Is.EqualTo(expected).Within(0.01f));
                Assert.That(blur, Is.GreaterThan(feedback.SevereBlurStartAmount));
                Assert.That(blur, Is.LessThan(GetMaxSeverePainBlur(feedback)));
                AssertPainEmotesExist(prototypes, feedback);
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    private static float GetExpectedPainBlur(CMUPainFeedbackComponent feedback, float equivalentPain)
    {
        const float severeThreshold = 60f;
        const float shockThreshold = 85f;
        var severeAmount = GetMaxSeverePainBlur(feedback);
        var progress = Math.Clamp(
            (equivalentPain - severeThreshold) / (shockThreshold - severeThreshold),
            0f,
            1f);

        return feedback.SevereBlurStartAmount +
            (severeAmount - feedback.SevereBlurStartAmount) * progress;
    }

    private static float GetMaxSeverePainBlur(CMUPainFeedbackComponent feedback)
    {
        const float severeBlurMax = 0.49f;
        return Math.Min(feedback.SevereBlurAmount, severeBlurMax);
    }

    [Test]
    public async Task PainSuppressionLoweringEffectiveTierPreventsFeedback()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pain = entMan.GetComponent<PainShockComponent>(human);
            SetPainState(entMan, human, pain, PainTier.Shock);
            entMan.System<SharedPainShockSystem>()
                .AddPainSuppressionProfile(human, 0f, 4, 0f, TimeSpan.FromSeconds(10));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var oldStatus = entMan.System<StatusEffectQuerySystem>();
            var damageable = entMan.GetComponent<DamageableComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(oldStatus.HasStatusEffect(human, "Stutter"), Is.False);
                Assert.That(oldStatus.HasStatusEffect(human, "Drunk"), Is.False);
                Assert.That(oldStatus.HasStatusEffect(human, "SlurredSpeech"), Is.False);
                Assert.That(entMan.HasComponent<BlurryVisionComponent>(human), Is.False);
                Assert.That(damageable.Damage.DamageDict["Asphyxiation"], Is.EqualTo(FixedPoint2.Zero));
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
            var oldStatus = entMan.System<StatusEffectQuerySystem>();
            var severeDamage = entMan.GetComponent<DamageableComponent>(severe).Damage.DamageDict["Asphyxiation"];
            var shockDamage = entMan.GetComponent<DamageableComponent>(shock).Damage.DamageDict["Asphyxiation"];
            var severeBlur = entMan.GetComponent<BlurryVisionComponent>(severe).Magnitude;
            var shockBlur = entMan.GetComponent<BlurryVisionComponent>(shock).Magnitude;

            Assert.Multiple(() =>
            {
                Assert.That(shockDamage, Is.GreaterThan(severeDamage));
                Assert.That(oldStatus.TryGetTime(severe, "Drunk", out _), Is.False);
                Assert.That(oldStatus.TryGetTime(shock, "Drunk", out var shockDrunkTime), Is.True);
                Assert.That(shockDrunkTime!.Value.Item2, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(oldStatus.HasStatusEffect(shock, "SlurredSpeech"), Is.True);
                Assert.That(oldStatus.HasStatusEffect(shock, "Stutter"), Is.True);
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

    private static void SetPainValue(IEntityManager entMan, EntityUid uid, FixedPoint2 painValue)
    {
        var pain = entMan.GetComponent<PainShockComponent>(uid);
        pain.Pain = painValue;
        pain.PainTarget = pain.Pain;
        pain.NextUpdate = TimeSpan.Zero;
        entMan.System<SharedPainShockSystem>().TickOne((uid, pain), refreshCache: false);
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

    private static void AssertPainEmotesExist(IPrototypeManager prototypes, CMUPainFeedbackComponent feedback)
    {
        foreach (var emote in feedback.SevereEmotes)
            Assert.That(prototypes.HasIndex<EmotePrototype>(emote), Is.True, $"{emote} must exist");

        foreach (var emote in feedback.ShockEmotes)
            Assert.That(prototypes.HasIndex<EmotePrototype>(emote), Is.True, $"{emote} must exist");
    }
}
