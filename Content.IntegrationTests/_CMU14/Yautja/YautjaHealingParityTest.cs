using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHealingParityTest
{
    [Test]
    public async Task YautjaMedicalApplicationsUseCmss13SoundContract()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var resources = server.ResolveDependency<IResourceManager>();
            var factory = server.EntMan.ComponentFactory;

            AssertStepAudio(
                prototypes,
                factory,
                resources,
                "CMUSurgeryStepMcompStabilizeWounds",
                ["/Audio/_CMU14/Yautja/Medical/clothingrustle1.ogg"],
                "/Audio/_RMC14/Medical/Surgery/cautery2.ogg",
                "/Audio/_RMC14/Medical/Surgery/organ1.ogg");
            AssertStepAudio(
                prototypes,
                factory,
                resources,
                "CMUSurgeryStepMcompTendWounds",
                [
                    "/Audio/_RMC14/Medical/Surgery/retractor1.ogg",
                    "/Audio/_CMU14/Yautja/Medical/heal_gun.ogg",
                ],
                "/Audio/_RMC14/Medical/Surgery/retractor2.ogg",
                "/Audio/_RMC14/Medical/Surgery/organ2.ogg");
            AssertStepAudio(
                prototypes,
                factory,
                resources,
                "CMUSurgeryStepMcompClampWound",
                ["/Audio/_RMC14/Medical/Surgery/cautery1.ogg"],
                "/Audio/_RMC14/Medical/Surgery/cautery2.ogg",
                "/Audio/Items/welder.ogg");

            var gunPrototype = prototypes.Index<EntityPrototype>("CMUYautjaHealingGun");
            Assert.That(gunPrototype.TryGetComponent<YautjaHealingGunComponent>(out var gun, factory), Is.True);
            AssertSoundPath(resources, gun!.ReloadSound, "/Audio/_RMC14/Medical/air_release.ogg");

            foreach (var injectorId in new[] { "CMUYautjaAutoInjector", "CMUYautjaThrallAutoInjector" })
            {
                var injector = prototypes.Index<EntityPrototype>(injectorId);
                Assert.That(injector.TryGetComponent<HyposprayComponent>(out var hypospray, factory), Is.True, injectorId);
                AssertSoundPath(
                    resources,
                    hypospray!.InjectSound,
                    "/Audio/_CMU14/Yautja/Medical/pred_crystal_inject.ogg");
            }

            foreach (var herbId in new[] { "CMUYautjaAdvancedBruisePack", "CMUYautjaAdvancedOintment" })
            {
                var herb = prototypes.Index<EntityPrototype>(herbId);
                Assert.That(herb.TryGetComponent<WoundTreaterComponent>(out var treater, factory), Is.True, herbId);
                Assert.Multiple(() =>
                {
                    Assert.That(treater!.TreatBeginSound, Is.Null, $"{herbId} has no application sound in CMSS13.");
                    Assert.That(treater.TreatEndSound, Is.Null, $"{herbId} has no application sound in CMSS13.");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StandingYautjaMedicompSelectionKeepsItsStandingPolicyDuringRevalidation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var damageable = entMan.System<DamageableSystem>();
            var skills = entMan.System<SkillsSystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var patient = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(patient, "RMCSkillSurgery", 1);
                damageable.TryChangeDamage(
                    patient,
                    new DamageSpecifier(prototypes.Index<DamageTypePrototype>("Blunt"), 20),
                    ignoreResistances: true);

                var parts = dispatch.BuildPartEntriesForSurgerySelection(
                    patient,
                    patient,
                    "CMUSurgeryMcompWounds");

                Assert.That(parts.SelectMany(part => part.EligibleSurgeries)
                    .Any(surgery => surgery.SurgeryId == "CMUSurgeryMcompWounds"), Is.True,
                    "The server must revalidate the standing Medicomp selection with the procedure's allowStanding policy.");
            }
            finally
            {
                if (!entMan.Deleted(patient))
                    entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SourceHealingContractIsPresent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var thwei = prototypes.Index<ReagentPrototype>("thwei");
            var dathwei = prototypes.Index<ReagentPrototype>("dathwei");

            AssertMedicineEffects(thwei, new()
            {
                ["Crossmetabolizing"] = 1,
                ["Antitoxic"] = 1,
                ["Yautjahemogenic"] = 9,
                ["YautjaWoundHealing"] = 9,
                ["Oxygenating"] = 6,
                ["Anticarcinogenic"] = 6,
                ["Bonemending"] = 6,
                ["Aiding"] = 1,
                ["Antihallucinogenic"] = 2,
                ["Focusing"] = 6,
                ["Curing"] = 4,
                ["Oculopeutic"] = 2,
                ["Neuropeutic"] = 2,
            });
            AssertMedicineEffects(dathwei, new()
            {
                ["Cardiostabilizing"] = 4,
                ["Organstabilizing"] = 1,
                ["Painkilling"] = 4,
                ["Antitoxic"] = 1,
                ["Yautjahemogenic"] = 3,
                ["YautjaWoundHealing"] = 3,
                ["Oxygenating"] = 6,
                ["Anticarcinogenic"] = 6,
                ["Bonemending"] = 6,
                ["Aiding"] = 1,
                ["Antihallucinogenic"] = 2,
                ["Focusing"] = 6,
                ["Curing"] = 4,
                ["Oculopeutic"] = 2,
                ["Neuropeutic"] = 2,
            });

            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            try
            {
                Assert.Multiple(() =>
                {
                Assert.That(thwei.Metabolisms, Is.Not.Null,
                    "CMSS13 thwei must have a real medicine metabolism, not only reagent metadata.");
                Assert.That(dathwei.Metabolisms, Is.Not.Null,
                    "CMSS13 dathwei must have a real medicine metabolism, not only reagent metadata.");
                Assert.That(entMan.GetComponent<YautjaBracerComponent>(bracer).HealingCapsuleCooldown,
                    Is.EqualTo(TimeSpan.FromMinutes(4)),
                    "CMSS13 healing capsules use a four-minute bracer cooldown.");
                Assert.That(entMan.GetComponent<YautjaBracerComponent>(bracer).HealingEnabled, Is.True,
                    "The CMSS13 bracer healing gate must default to enabled.");
                });
            }
            finally
            {
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }

            var stabilizer = prototypes.Index<EntityPrototype>("CMUYautjaStabilizerGel");
            var gun = prototypes.Index<EntityPrototype>("CMUYautjaHealingGun");
            var clamp = prototypes.Index<EntityPrototype>("CMUYautjaWoundClamp");

            Assert.Multiple(() =>
            {
                Assert.That(stabilizer.TryGetComponent<CMSurgeryToolComponent>(out _), Is.True,
                    "The stabilizer gel must be a surgery tool in the CMU surgery flow.");
                Assert.That(gun.TryGetComponent<CMSurgeryToolComponent>(out _), Is.True,
                    "The healing gun must be a surgery tool in the CMU surgery flow.");
                Assert.That(clamp.TryGetComponent<CMSurgeryToolComponent>(out _), Is.True,
                    "The wound clamp must be a surgery tool in the CMU surgery flow.");
            });

            var surgeryFlow = entMan.System<SharedCMUSurgeryFlowSystem>();
            Assert.That(surgeryFlow.TryGetDefinition("CMUSurgeryMcompWounds", out var surgery), Is.True,
                "The CMSS13 three-step Medicomp procedure must be registered as a surgery.");
            Assert.Multiple(() =>
            {
                Assert.That(surgery.AllowSelfSurgery, Is.True);
                Assert.That(surgery.AllowStanding, Is.True);
                Assert.That(surgery.RequiresYautjaTech, Is.True);
                Assert.That(surgery.Steps.Select(step => step.DoAfterSeconds).ToArray(),
                    Is.EqualTo(new float?[] { 5f, 15f, 10f }),
                    "Medicomp stages must retain CMSS13's 5/15/10 second timings.");
            });

            var sleeper = prototypes.Index<EntityPrototype>("CMUHunterShipPlacedCMUAutodocPodSleeperSouthOffset0x8");
            Assert.That(sleeper.TryGetComponent<CMUAutodocPodComponent>(out var sleeperConfig), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sleeperConfig.AvailableChemicals.Select(id => id.Id), Is.EquivalentTo(new[]
                {
                    "thwei", "CMInaprovaline", "CMUOxycodone", "CMDylovene", "CMDexalinPlus",
                    "CMTricordrazine", "CMAlkysine", "CMImidazoline",
                }));
                Assert.That(sleeperConfig.EmergencyChemicals.Select(id => id.Id), Is.EquivalentTo(new[]
                {
                    "thwei", "CMInaprovaline", "CMUOxycodone", "CMDylovene", "CMDexalinPlus",
                    "CMTricordrazine", "CMBicaridine", "CMKelotane", "CMMeralyne", "CMDermaline",
                    "CMAlkysine", "CMImidazoline",
                }));
                Assert.That(sleeperConfig.MaxChemicalVolume, Is.EqualTo(FixedPoint2.New(40)));
                Assert.That(sleeperConfig.DialysisRatePerSecond, Is.EqualTo(FixedPoint2.New(8)));
                Assert.That(sleeperConfig.ChemicalDose, Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(sleeperConfig.LargeChemicalDose, Is.EqualTo(FixedPoint2.New(10)));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertMedicineEffects(ReagentPrototype reagent, Dictionary<string, float> expected)
    {
        Assert.That(reagent.Metabolisms, Is.Not.Null, reagent.ID);
        Assert.That(reagent.Metabolisms!.TryGetValue("Medicine", out var medicine), Is.True, reagent.ID);
        Assert.That(medicine!.Effects.Length, Is.EqualTo(expected.Count), reagent.ID);

        foreach (var (typeName, potency) in expected)
        {
            var effect = medicine.Effects.SingleOrDefault(candidate => candidate.GetType().Name == typeName);
            Assert.That(effect, Is.InstanceOf<RMCChemicalEffect>(), $"{reagent.ID} should contain {typeName}.");
            Assert.That(((RMCChemicalEffect) effect!).Potency, Is.EqualTo(potency),
                $"{reagent.ID} {typeName} potency should match CMSS13.");
        }
    }

    private static void AssertStepAudio(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        IResourceManager resources,
        string stepId,
        string[] startPaths,
        string successPath,
        string failurePath)
    {
        var step = prototypes.Index<EntityPrototype>(stepId);
        Assert.That(step.TryGetComponent<CMUSurgeryStepAudioComponent>(out var audio, factory), Is.True, stepId);
        Assert.That(audio!.StartSounds, Has.Count.EqualTo(startPaths.Length), stepId);

        for (var i = 0; i < startPaths.Length; i++)
            AssertSoundPath(resources, audio.StartSounds[i], startPaths[i]);

        AssertSoundPath(resources, audio.SuccessSound, successPath);
        AssertSoundPath(resources, audio.FailureSound, failurePath);
    }

    private static void AssertSoundPath(IResourceManager resources, SoundSpecifier? sound, string expectedPath)
    {
        Assert.That(sound, Is.TypeOf<SoundPathSpecifier>());
        var actual = ((SoundPathSpecifier) sound!).Path;
        Assert.Multiple(() =>
        {
            Assert.That(actual, Is.EqualTo(new ResPath(expectedPath)));
            Assert.That(resources.ContentFileExists(actual), Is.True, actual.ToString());
        });

        using var stream = resources.ContentFileRead(actual);
        var header = new byte[512];
        var length = stream.Read(header, 0, header.Length);
        ReadOnlySpan<byte> marker = [0x01, (byte) 'v', (byte) 'o', (byte) 'r', (byte) 'b', (byte) 'i', (byte) 's'];
        var channelCount = -1;
        for (var i = 0; i <= length - marker.Length - 5; i++)
        {
            if (!header.AsSpan(i, marker.Length).SequenceEqual(marker))
                continue;

            channelCount = header[i + marker.Length + sizeof(uint)];
            break;
        }

        Assert.That(channelCount, Is.EqualTo(1),
            $"{actual} is played positionally by the RMC/CMU medical code and must be mono.");
    }
}
