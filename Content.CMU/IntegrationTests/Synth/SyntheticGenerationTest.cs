using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Humanoid;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Humanoid.Markings;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.CMU14.Synth;
using Content.Shared.CombatMode;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Synth;

[TestFixture]
public sealed class SyntheticGenerationTest : GameTest
{
    [Test]
    public async Task SpawnAppliesSelectedGenerationAfterEveryFactionJobProfile()
    {
        const int generation = 4;
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithSynthetic(true).WithSyntheticGeneration(generation);
            var spawning = Server.System<StationSpawningSystem>();
            foreach (var job in new[]
                     {
                         "AU14JobCivilianColonySynthetic", "AU14JobGOVFORAuxSupportSynth",
                         "AU14JobOPFORAuxSupportSynth", "AU14JobGOVFORAuxSupportSynthRMC",
                         "AU14JobGOVFORAuxSupportSynthUPP",
                     })
            {
                var mob = spawning.SpawnPlayerMob(map.GridCoords, job, profile, null);
                Assert.That(SEntMan.GetComponent<CMUSynthGenerationComponent>(mob).Generation, Is.EqualTo(generation), job);
                SEntMan.DeleteEntity(mob);
            }
        });
    }

    [Test]
    public async Task GenerationSetupPreservesShieldProvidedLeapProtection()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var synth = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var synthetic = SEntMan.EnsureComponent<SynthComponent>(synth);
            var provider = SEntMan.SpawnEntity(null, map.GridCoords);
            var shield = SEntMan.EnsureComponent<RMCGrantLeapProtectionComponent>(provider);
            shield.StunDuration = TimeSpan.FromSeconds(2);
            var protection = SEntMan.GetComponent<RMCLeapProtectionComponent>(synth);
            protection.ProtectionProviders.Add(provider);

            Server.System<SharedSynthSystem>().ConfigureGeneration((synth, synthetic), 4);
            Assert.That(SEntMan.HasComponent<RMCLeapProtectionComponent>(synth), Is.True);
            Assert.That(protection.InherentStunDuration, Is.Null);
            Assert.That(protection.StunDuration, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(protection.ProtectionProviders, Does.Contain(provider));
            SEntMan.DeleteEntity(synth);
            SEntMan.DeleteEntity(provider);
        });
    }

    [Test]
    public async Task FixedWorkingJoesAndNonSyntheticJobsIgnoreSelectedGeneration()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var spawning = Server.System<StationSpawningSystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithSyntheticGeneration(4);
            var joe = spawning.SpawnPlayerMob(map.GridCoords, "AU14JobColonyWorkingJoe", profile, null);
            Assert.That(SEntMan.HasComponent<CMUSynthGenerationComponent>(joe), Is.False);
            Assert.That(Server.System<MobThresholdSystem>().TryGetThresholdForState(joe, MobState.Dead, out var death), Is.True);
            Assert.That(death, Is.EqualTo(FixedPoint2.New(300)));
            Assert.That(SEntMan.GetComponent<SynthComponent>(joe).Generation,
                Is.EqualTo("rmc-species-synth-generation-first"));
            var human = spawning.SpawnPlayerMob(map.GridCoords, "AU14JobCivilianColonist", profile, null);
            Assert.That(SEntMan.HasComponent<CMUSynthGenerationComponent>(human), Is.False);
            SEntMan.DeleteEntity(joe);
            SEntMan.DeleteEntity(human);
        });
    }

    [Test]
    public async Task FirstGenerationSyntheticHasWhiteSkinAndCombatModeEyeColor()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var eyeColor = Color.FromHex("#123456");
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            profile = profile.WithCharacterAppearance(profile.Appearance.WithEyeColor(eyeColor))
                .WithSynthetic(true)
                .WithSyntheticGeneration(1);
            var synth = Server.System<StationSpawningSystem>()
                .SpawnPlayerMob(map.GridCoords, "AU14JobCivilianColonySynthetic", profile, null);
            var appearance = Server.System<HumanoidOrganAppearanceSystem>();

            Assert.That(appearance.TryGetColors(synth, out var skinColor, out var actualEyeColor), Is.True);
            Assert.That(skinColor, Is.EqualTo(Color.White));
            Assert.That(actualEyeColor, Is.EqualTo(eyeColor));
            Assert.That(SEntMan.GetComponent<RMCIntentsEyeColorComponent>(synth).EyeColorHelp, Is.EqualTo(eyeColor));

            var enableCombatMode = new ToggleCombatActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(synth, ref enableCombatMode);
            Assert.That(appearance.TryGetColors(synth, out _, out actualEyeColor), Is.True);
            Assert.That(actualEyeColor, Is.EqualTo(Color.FromHex("#ff0000")));

            var disableCombatMode = new ToggleCombatActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(synth, ref disableCombatMode);
            Assert.That(appearance.TryGetColors(synth, out _, out actualEyeColor), Is.True);
            Assert.That(actualEyeColor, Is.EqualTo(eyeColor));
            SEntMan.DeleteEntity(synth);
        });
    }

#pragma warning disable RA0002 // Construct transient leap origins and times to exercise directional and probability boundaries.
    [Test]
    public async Task OnlyConfiguredGenerationBlocksFrontalRunnerPounces()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var spawning = Server.System<StationSpawningSystem>();
            var earlyProfile = HumanoidCharacterProfile.DefaultWithSpecies().WithSynthetic(true).WithSyntheticGeneration(1);
            var lateProfile = HumanoidCharacterProfile.DefaultWithSpecies().WithSynthetic(true).WithSyntheticGeneration(4);
            var earlySynth = spawning.SpawnPlayerMob(map.GridCoords, "AU14JobCivilianColonySynthetic", earlyProfile, null);
            var lateSynth = spawning.SpawnPlayerMob(map.GridCoords, "AU14JobCivilianColonySynthetic", lateProfile, null);
            var xeno = SEntMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(0, -3)));
            Server.System<SharedTransformSystem>().SetWorldRotation(earlySynth, Angle.Zero);
            Server.System<SharedTransformSystem>().SetWorldRotation(lateSynth, Angle.Zero);
            var leap = SEntMan.EnsureComponent<XenoLeapingComponent>(xeno);
            leap.Origin = map.GridCoords.Offset(new Vector2(0, -3));
            var earlyBlocked = false;
            const int attempts = 512;
            for (var i = 0; i < attempts; i++)
            {
                leap.LeapEndTime = TimeSpan.FromSeconds(i + 1);
                var attempt = new XenoLeapHitAttempt(xeno);
                SEntMan.EventBus.RaiseLocalEvent(earlySynth, ref attempt);
                var repeated = new XenoLeapHitAttempt(xeno);
                SEntMan.EventBus.RaiseLocalEvent(earlySynth, ref repeated);
                Assert.That(repeated.Cancelled, Is.EqualTo(attempt.Cancelled), "Prediction must not reroll the same leap.");
                earlyBlocked |= attempt.Cancelled;

                var lateAttempt = new XenoLeapHitAttempt(xeno);
                SEntMan.EventBus.RaiseLocalEvent(lateSynth, ref lateAttempt);
                Assert.That(lateAttempt.Cancelled, Is.False, "Later generations must not block frontal pounces.");
            }

            Assert.That(earlyBlocked, Is.True, "First-generation synthetics should sometimes block frontal pounces.");

            leap.Origin = map.GridCoords.Offset(new Vector2(0, 3));
            for (var i = 0; i < 16; i++)
            {
                leap.LeapEndTime = TimeSpan.FromSeconds(i + 1);
                var attempt = new XenoLeapHitAttempt(xeno);
                SEntMan.EventBus.RaiseLocalEvent(earlySynth, ref attempt);
                Assert.That(attempt.Cancelled, Is.False, "A rear pounce must never be blocked by generation mechanics.");
            }

            SEntMan.DeleteEntity(xeno);
            SEntMan.DeleteEntity(earlySynth);
            SEntMan.DeleteEntity(lateSynth);
        });
    }
#pragma warning restore RA0002

    [Test]
    public async Task GenerationAndMovementReplicateToTheClient()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        NetEntity netSynth = default;
        float gearWalkSpeed = 0;
        var previous = ServerSession!.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithSynthetic(true).WithSyntheticGeneration(4);
                synth = Server.System<StationSpawningSystem>()
                    .SpawnPlayerMob(map.GridCoords, "AU14JobCivilianColonySynthetic", profile, null);
                netSynth = SEntMan.GetNetEntity(synth);
                gearWalkSpeed = SEntMan.GetComponent<MovementSpeedModifierComponent>(synth).WalkSpeedModifier;
                Server.PlayerMan.SetAttachedEntity(ServerSession, synth);
            });
            await Pair.RunSeconds(1);
            await Client.WaitAssertion(() =>
            {
                var clientSynth = CEntMan.GetEntity(netSynth);
                Assert.That(CEntMan.GetComponent<CMUSynthGenerationComponent>(clientSynth).Generation, Is.EqualTo(4));
                var speed = new RefreshMovementSpeedModifiersEvent();
                CEntMan.EventBus.RaiseLocalEvent(clientSynth, speed);
                Assert.That(speed.WalkSpeedModifier, Is.EqualTo(gearWalkSpeed).Within(0.001));
            });

            await Server.WaitAssertion(() =>
            {
                Server.System<SharedSynthSystem>().ConfigureGeneration((synth, SEntMan.GetComponent<SynthComponent>(synth)), 1);
            });
            await Pair.RunSeconds(1);
            await Client.WaitAssertion(() =>
            {
                var clientSynth = CEntMan.GetEntity(netSynth);
                Assert.That(CEntMan.GetComponent<CMUSynthGenerationComponent>(clientSynth).Generation, Is.EqualTo(1));
                var speedModifier = CEntMan.GetComponent<CMUSynthGenerationComponent>(clientSynth).MovementSpeedModifier;
                var speed = new RefreshMovementSpeedModifiersEvent();
                CEntMan.EventBus.RaiseLocalEvent(clientSynth, speed);
                Assert.That(speed.WalkSpeedModifier, Is.EqualTo(gearWalkSpeed * speedModifier).Within(0.001));
                Assert.That(CEntMan.GetComponent<MovementSpeedModifierComponent>(clientSynth).WalkSpeedModifier,
                    Is.EqualTo(gearWalkSpeed * 0.85f).Within(0.001));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(ServerSession, previous);
                if (SEntMan.EntityExists(synth))
                    SEntMan.DeleteEntity(synth);
            });
        }
    }

    [Test]
    public void ProfileAllowsAllFourGenerationsAndClampsUnsupportedValues()
    {
        foreach (var (input, expected) in new[] { (0, 1), (1, 1), (2, 2), (3, 3), (4, 4), (5, 4) })
            Assert.That(new HumanoidCharacterProfile().WithSyntheticGeneration(input).SyntheticGeneration, Is.EqualTo(expected));
    }
}
