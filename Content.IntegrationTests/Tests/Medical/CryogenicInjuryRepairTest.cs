#pragma warning disable RA0002 // Seed controlled medical injury states for treatment regression tests.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Temperature.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
public sealed class CryogenicInjuryRepairTest : GameTest
{
    [Test]
    public async Task NecrosolMendsEveryFractureAndRegeneratesBloodByOnePercent()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var patient = SpawnPatient(map.GridCoords);
            var parts = Server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(patient)
                .Where(part => SEntMan.HasComponent<BoneComponent>(part.Owner))
                .Take(2).ToArray();
            Assert.That(parts.Length, Is.EqualTo(2));

            var bones = Server.System<SharedBoneSystem>();
            Assert.That(bones.SeedFracture(parts[0].Owner, FractureSeverity.Hairline), Is.True);
            Assert.That(bones.SeedFracture(parts[1].Owner, FractureSeverity.Shattered), Is.True);
            var first = SEntMan.GetComponent<BoneComponent>(parts[0].Owner);
            var second = SEntMan.GetComponent<BoneComponent>(parts[1].Owner);
            var firstBefore = first.Integrity;
            var secondBefore = second.Integrity;

            var bloodstream = Server.System<BloodstreamSystem>();
            var blood = SEntMan.GetComponent<BloodstreamComponent>(patient);
            var normalVolume = bloodstream.GetReferenceVolume((patient, blood));
            Assert.That(bloodstream.TryModifyBloodLevel((patient, blood), -normalVolume / 2), Is.True);
            var before = bloodstream.GetBloodLevel((patient, blood));

            Assert.That(ApplyDose<CMUCryogenicBoneAndBloodRepair>(patient, "Necrosol", 1f), Is.True);
            Assert.That(first.Integrity, Is.GreaterThan(firstBefore));
            Assert.That(second.Integrity, Is.GreaterThan(secondBefore));
            Assert.That(bloodstream.GetBloodLevel((patient, blood)), Is.EqualTo(before + 0.01f).Within(0.001f));
        });
    }

    [Test]
    public async Task BurnAndBruteMedicinesCloseOnlyTheirOwnWounds()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var patient = SpawnPatient(map.GridCoords);
            var part = Server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(patient).First().Owner;
            var wounds = SEntMan.AddComponent<BodyPartWoundComponent>(part);
            var ledger = Server.System<CMUWoundLedgerSystem>();
            ledger.AddEntry(wounds, Entry(WoundType.Burn, WoundSize.BurnSevere));
            ledger.AddEntry(wounds, Entry(WoundType.Brute, WoundSize.CutGaping));
            SEntMan.AddComponent<CMUEscharComponent>(part);

            Assert.That(ApplyDose<CMUCryogenicWoundRepair>(patient, "Aloxadone", 1f), Is.True);
            Assert.That(ledger.GetEntries(wounds).Single(e => e.Wound.Type == WoundType.Burn).Wound.Healed,
                Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(ledger.GetEntries(wounds).Single(e => e.Wound.Type == WoundType.Brute).Wound.Healed,
                Is.EqualTo(FixedPoint2.Zero));
            Assert.That(SEntMan.HasComponent<CMUEscharComponent>(part), Is.True);

            Assert.That(ApplyDose<CMUCryogenicWoundRepair>(patient, "Traumoxadone", 1f), Is.True);
            Assert.That(ledger.GetEntries(wounds).Single(e => e.Wound.Type == WoundType.Brute).Wound.Healed,
                Is.GreaterThan(FixedPoint2.Zero));

            ApplyDose<CMUCryogenicWoundRepair>(patient, "Aloxadone", 120f);
            Assert.That(ledger.GetEntries(wounds).Any(e => e.Wound.Type == WoundType.Burn), Is.False);
            Assert.That(SEntMan.HasComponent<CMUEscharComponent>(part), Is.False);
            Assert.That(ledger.GetEntries(wounds).Any(e => e.Wound.Type == WoundType.Brute), Is.True);
        });
    }

    [Test]
    public async Task DoxarubixadoneRepairsEveryAttachedOrganOnlyWhenCold()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var patient = SpawnPatient(map.GridCoords);
            var organs = Server.System<CMUMedicalBodyIndexSystem>().GetOrgans(patient)
                .Select(organ => SEntMan.GetComponentOrNull<OrganHealthComponent>(organ.Owner))
                .Where(health => health != null).Take(2).ToArray();
            Assert.That(organs.Length, Is.EqualTo(2));
            foreach (var health in organs)
                health!.Current = health.Max - 10;

            SEntMan.GetComponent<TemperatureComponent>(patient).Temperature = 214f;
            Assert.That(ApplyDose<CMUCryogenicOrganRepair>(patient, "Doxarubixadone", 1f), Is.False);
            foreach (var health in organs)
                Assert.That(health!.Current, Is.EqualTo(health.Max - 10));

            SEntMan.GetComponent<TemperatureComponent>(patient).Temperature = 213f;
            Assert.That(ApplyDose<CMUCryogenicOrganRepair>(patient, "Doxarubixadone", 1f), Is.True);
            foreach (var health in organs)
                Assert.That(health!.Current, Is.GreaterThan(health.Max - 10));
        });
    }

    private EntityUid SpawnPatient(EntityCoordinates coordinates)
    {
        var patient = SEntMan.SpawnEntity("CMMobHuman", coordinates);
        SEntMan.GetComponent<TemperatureComponent>(patient).Temperature = 213f;
        return patient;
    }

    private static CMUWoundEntry Entry(WoundType type, WoundSize size)
        => new(new Wound(30, FixedPoint2.Zero, 0f, null, type, false),
            size, 0, type == WoundType.Burn ? WoundMechanism.Burn : WoundMechanism.Slash,
            WoundMechanismFlags.None, WoundTreatmentQuality.Untreated, WoundCleanupFlags.None);

    private bool ApplyDose<TEffect>(EntityUid patient, ProtoId<ReagentPrototype> reagentId, float dose)
        where TEffect : EntityEffect
    {
        var reagent = SProtoMan.Index(reagentId);
        var effect = reagent.Metabolisms!.Metabolisms["Bloodstream"].Effects.OfType<TEffect>().Single();
        var context = new ReagentEffectContext(reagent, null, null, null,
            new ReagentQuantity(reagentId, FixedPoint2.New(dose)), "Bloodstream", null,
            ReagentEffectOrigin.Metabolism);
        return Server.System<SharedEntityEffectsSystem>().TryApplyEffect(patient, effect,
            scale: dose, reagentContext: context);
    }
}
