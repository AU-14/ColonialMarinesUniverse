using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.TemporaryBlurryVision;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class PainPlayerFeedbackTest
{
    [Test]
    public async Task SeverePainAppliesPlayerFacingFeedback()
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
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var feedback = entMan.GetComponent<CMUPainFeedbackComponent>(human);
            var damageable = entMan.GetComponent<DamageableComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(oldStatus.HasStatusEffect(human, "Stutter"), Is.True);
                Assert.That(oldStatus.HasStatusEffect(human, "Drunk"), Is.True);
                Assert.That(oldStatus.HasStatusEffect(human, "SlurredSpeech"), Is.False);
                Assert.That(entMan.HasComponent<BlurryVisionComponent>(human), Is.True);
                Assert.That(entMan.GetComponent<BlurryVisionComponent>(human).Magnitude,
                    Is.EqualTo(feedback.SevereBlurStartAmount));
                Assert.That(feedback.SevereBlurStartAmount,
                    Is.LessThanOrEqualTo(0.2f),
                    "Severe pain should start the cataracts barely visible instead of snapping to the full severe amount.");
                Assert.That(damageable.Damage.DamageDict["Asphyxiation"], Is.GreaterThan(FixedPoint2.Zero));
                AssertPainEmotesExist(prototypes, feedback);
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
                Assert.That(entMan.HasComponent<BlurryVisionComponent>(human), Is.True);
                Assert.That(entMan.GetComponent<BlurryVisionComponent>(human).Magnitude, Is.EqualTo(0.02f).Within(0.001f));
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
            SetPainValue(entMan, human, (FixedPoint2)61);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var feedback = entMan.GetComponent<CMUPainFeedbackComponent>(human);
            var blur = entMan.GetComponent<BlurryVisionComponent>(human).Magnitude;
            const float expectedProgress = (61f - 60f) / (85f - 60f);
            var expected = feedback.SevereBlurStartAmount +
                (feedback.ShockBlurStartAmount - feedback.SevereBlurStartAmount) * expectedProgress;

            Assert.Multiple(() =>
            {
                Assert.That(blur, Is.EqualTo(expected).Within(0.01f));
                Assert.That(blur, Is.GreaterThan(feedback.SevereBlurStartAmount));
                Assert.That(blur, Is.LessThan(0.2f));
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
            var blur = entMan.GetComponent<BlurryVisionComponent>(human).Magnitude;
            const float expectedProgress = (90f - 85f) / (95f - 85f);
            var expected = feedback.ShockBlurStartAmount +
                (feedback.ShockBlurAmount - feedback.ShockBlurStartAmount) * expectedProgress;

            Assert.Multiple(() =>
            {
                Assert.That(blur, Is.EqualTo(expected).Within(0.01f));
                Assert.That(blur, Is.GreaterThan(feedback.ShockBlurStartAmount));
                Assert.That(blur, Is.LessThan(feedback.ShockBlurAmount));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
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
                Assert.That(oldStatus.TryGetTime(severe, "Drunk", out var severeDrunkTime), Is.True);
                Assert.That(oldStatus.TryGetTime(shock, "Drunk", out var shockDrunkTime), Is.True);
                Assert.That(shockDrunkTime!.Value.Item2, Is.GreaterThan(severeDrunkTime!.Value.Item2));
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
            PainTier.Mild => PainTierThresholds.UpwardThresholds[0],
            PainTier.Moderate => PainTierThresholds.UpwardThresholds[1],
            PainTier.Severe => PainTierThresholds.UpwardThresholds[2],
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
