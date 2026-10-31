using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.CMU14.Synth;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
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
    [TestCase(1, 0.85f, 250, 300, 10)]
    [TestCase(2, 0.85f, 250, 300, 10)]
    [TestCase(3, 1f, 200, 250, 15)]
    [TestCase(4, 1f, 200, 250, 15)]
    public async Task SpawnAppliesGenerationAfterEveryFactionJobProfile(
        int generation, float speed, int critical, int dead, int acidDamage)
    {
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
                Assert.That(SEntMan.HasComponent<RMCLeapProtectionComponent>(mob), Is.False,
                    $"{job} must not retain the old guaranteed innate pounce block.");
                var thresholds = Server.System<MobThresholdSystem>();
                Assert.That(thresholds.TryGetThresholdForState(mob, MobState.Critical, out var crit), Is.True, job);
                Assert.That(crit, Is.EqualTo(FixedPoint2.New(critical)), job);
                Assert.That(thresholds.TryGetThresholdForState(mob, MobState.Dead, out var death), Is.True, job);
                Assert.That(death, Is.EqualTo(FixedPoint2.New(dead)), job);

                var movement = new RefreshMovementSpeedModifiersEvent();
                SEntMan.EventBus.RaiseLocalEvent(mob, movement);
                // Compare against the same equipped body without the generation modifier; job gear has its own slowdown.
                SEntMan.RemoveComponent<CMUSynthGenerationComponent>(mob);
                var gearSpeed = new RefreshMovementSpeedModifiersEvent();
                SEntMan.EventBus.RaiseLocalEvent(mob, gearSpeed);
                Server.System<SharedSynthSystem>().ConfigureGeneration((mob, SEntMan.GetComponent<SynthComponent>(mob)), generation);
                Assert.That(movement.WalkSpeedModifier, Is.EqualTo(gearSpeed.WalkSpeedModifier * speed).Within(0.001), job);
                Assert.That(movement.SprintSpeedModifier, Is.EqualTo(gearSpeed.SprintSpeedModifier * speed).Within(0.001), job);
                Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(mob).WalkSpeedModifier,
                    Is.EqualTo(movement.WalkSpeedModifier).Within(0.001), job);

                var skills = Server.System<SkillsSystem>();
                foreach (var (skill, earlyLevel, advancedLevel) in new[]
                         {
                             ("RMCSkillCqc", 4, 5), ("RMCSkillEndurance", 0, 1),
                             ("RMCSkillEngineer", 3, 4), ("RMCSkillConstruction", 2, 3),
                             ("RMCSkillFirearms", 1, 2), ("RMCSkillOverwatch", 0, 1),
                             ("RMCSkillMedical", 3, 4), ("RMCSkillSurgery", 3, 4),
                             ("RMCSkillResearch", 0, 1), ("RMCSkillMeleeWeapons", 1, 2),
                             ("RMCSkillPilot", 1, 2), ("RMCSkillPolice", 1, 2),
                             ("RMCSkillFireman", 4, 5), ("RMCSkillPowerLoader", 1, 2),
                             ("RMCSkillVehicles", 1, 2), ("RMCSkillJtac", 2, 3),
                             ("RMCSkillIntel", 1, 2), ("RMCSkillDomestics", 1, 2),
                             ("RMCSkillNavigations", 0, 1),
                         })
                {
                    Assert.That(skills.GetSkill(mob, new EntProtoId<SkillDefinitionComponent>(skill)),
                        Is.EqualTo(generation <= 2 ? earlyLevel : advancedLevel), $"{job}: {skill}");
                }

                var damage = Server.System<DamageableSystem>().TryChangeDamage(mob,
                    new DamageSpecifier { DamageDict = { ["Caustic"] = 10 } });
                Assert.That(damage!.GetTotal(), Is.EqualTo(FixedPoint2.New(acidDamage)), job);
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

#pragma warning disable RA0002 // Construct transient leap origins and times to exercise directional and probability boundaries.
    [TestCase(1, "CMXenoRunner", true)]
    [TestCase(1, "CMXenoLurker", true)]
    [TestCase(2, "CMXenoRunner", true)]
    [TestCase(2, "CMXenoLurker", true)]
    [TestCase(3, "CMXenoRunner", false)]
    [TestCase(4, "CMXenoLurker", false)]
    [TestCase(1, "CMXenoRavager", false)]
    [TestCase(2, "CMXenoParasite", false)]
    public async Task OnlyEarlyGenerationsBlockFrontalRunnerAndLurkerPounces(int generation, string caste, bool canBlock)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithSynthetic(true).WithSyntheticGeneration(generation);
            var synth = Server.System<StationSpawningSystem>()
                .SpawnPlayerMob(map.GridCoords, "AU14JobCivilianColonySynthetic", profile, null);
            var xeno = SEntMan.SpawnEntity(caste, map.GridCoords.Offset(new Vector2(0, -3)));
            Server.System<SharedTransformSystem>().SetWorldRotation(synth, Angle.Zero);
            var leap = SEntMan.EnsureComponent<XenoLeapingComponent>(xeno);
            leap.Origin = map.GridCoords.Offset(new Vector2(0, -3));
            var blocked = 0;
            const int attempts = 2000;
            for (var i = 0; i < attempts; i++)
            {
                leap.LeapEndTime = TimeSpan.FromSeconds(i + 1);
                var attempt = new XenoLeapHitAttempt(xeno);
                SEntMan.EventBus.RaiseLocalEvent(synth, ref attempt);
                var repeated = new XenoLeapHitAttempt(xeno);
                SEntMan.EventBus.RaiseLocalEvent(synth, ref repeated);
                Assert.That(repeated.Cancelled, Is.EqualTo(attempt.Cancelled), "Prediction must not reroll the same leap.");
                if (attempt.Cancelled)
                    blocked++;
            }

            if (canBlock)
                Assert.That(blocked / (float) attempts, Is.InRange(0.11f, 0.19f), "Frontal pounces should be blocked 15% of the time.");
            else
                Assert.That(blocked, Is.Zero, "Other generations and other castes must never get this block.");

            leap.Origin = map.GridCoords.Offset(new Vector2(0, 3));
            for (var i = 0; i < attempts; i++)
            {
                leap.LeapEndTime = TimeSpan.FromSeconds(i + 1);
                var attempt = new XenoLeapHitAttempt(xeno);
                SEntMan.EventBus.RaiseLocalEvent(synth, ref attempt);
                Assert.That(attempt.Cancelled, Is.False, "A rear pounce must never be blocked by generation mechanics.");
            }

            SEntMan.DeleteEntity(xeno);
            SEntMan.DeleteEntity(synth);
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
                var speed = new RefreshMovementSpeedModifiersEvent();
                CEntMan.EventBus.RaiseLocalEvent(clientSynth, speed);
                Assert.That(speed.WalkSpeedModifier, Is.EqualTo(gearWalkSpeed * 0.85f).Within(0.001));
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
