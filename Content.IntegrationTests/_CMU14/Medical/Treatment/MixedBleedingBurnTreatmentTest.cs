#pragma warning disable RA0002 // Observe operation identity and isolate blood regeneration in the blood-drain fixture.
using Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Treatment;

/// <summary>Actual shipped items must select independent bleeding and preserve each wound's treatment type.</summary>
[TestFixture]
public sealed class MixedBleedingBurnTreatmentTest
{
    [TestCase(ExternalBleedTier.Minor)]
    [TestCase(ExternalBleedTier.Moderate)]
    [TestCase(ExternalBleedTier.Severe)]
    public async Task OrdinaryGauzeStopsNonarterialBleedingWithNoWoundsOrBruteDamage(ExternalBleedTier tier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em);
                SetBleeding(em, rig.Part, tier);
                Assert.That(Rows(em, rig.Part), Is.Empty);
                Assert.That(em.System<DamageableSystem>().GetTotalDamage(rig.Patient), Is.EqualTo(FixedPoint2.Zero));
                Interact(em, rig, rig.Tool);
                AssertOperation(em, rig, rig.Tool);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.None));
                Assert.That(Rows(em, rig.Part), Is.Empty);
                Assert.That(em.System<DamageableSystem>().GetTotalDamage(rig.Patient), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(9));
                Assert.That(em.HasComponent<CMUBandagePendingComponent>(rig.Medic), Is.False);
            });
        }
        finally { await pair.Server.WaitPost(() => Delete(em, rig)); }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BareOintmentCannotStopIndependentBleedingWithoutABurnToTreat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, "CMOintment1");
                SetBleeding(em, rig.Part, ExternalBleedTier.Minor);
                Interact(em, rig, rig.Tool);
                Assert.That(em.HasComponent<CMUBandagePendingComponent>(rig.Medic), Is.False);
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.Minor));
                Assert.That(Rows(em, rig.Part), Is.Empty);
                Assert.That(Count(em, rig.Tool), Is.EqualTo(1));
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.Minor));
                Assert.That(Rows(em, rig.Part), Is.Empty);
                Assert.That(Count(em, rig.Tool), Is.EqualTo(1));
                Assert.That(em.System<DamageableSystem>().GetTotalDamage(rig.Patient), Is.EqualTo(FixedPoint2.Zero));
            });
        }
        finally { await pair.Server.WaitPost(() => Delete(em, rig)); }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GauzeStopsIndependentBleedingOnABurnOnlySiteThenOintmentTreatsTheBurn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        EntityUid ointment = default;
        CMUWoundEntry burnBefore = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em);
                Hit(em, rig.Patient, rig.Part, "Heat", 10);
                SetBleeding(em, rig.Part, ExternalBleedTier.Moderate);
                burnBefore = Rows(em, rig.Part).Single();
                Assert.That(burnBefore.Wound.Type, Is.EqualTo(WoundType.Burn));
                Assert.That(Damage(em, rig.Patient, "Slash"), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Damage(em, rig.Patient, "Blunt"), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Damage(em, rig.Patient, "Piercing"), Is.EqualTo(FixedPoint2.Zero));
                Interact(em, rig, rig.Tool);
                AssertOperation(em, rig, rig.Tool);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.None));
                AssertUnchangedWoundBurden(Rows(em, rig.Part).Single(), burnBefore);
                Assert.That(Damage(em, rig.Patient, "Heat"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Debt(em, rig.Part, "Heat"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(9));
                ointment = HeldTool(em, rig.Medic, "CMOintment10");
                Interact(em, rig, ointment);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Rows(em, rig.Part).Single().Wound.Treated, Is.True);
                Assert.That(Damage(em, rig.Patient, "Heat"), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Debt(em, rig.Part, "Heat"), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Count(em, ointment), Is.EqualTo(9));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(9));
            });
        }
        finally { await pair.Server.WaitPost(() => { Delete(em, ointment); Delete(em, rig); }); }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MixedSlashAndHeatWoundsKeepTheirOwnTreatmentInEitherOrder(bool ointmentFirst)
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        EntityUid ointment = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em);
                Hit(em, rig.Patient, rig.Part, "Slash", 10);
                Hit(em, rig.Patient, rig.Part, "Heat", 10);
                Assert.That(Rows(em, rig.Part), Has.Count.EqualTo(2));
                ointment = HeldTool(em, rig.Medic, "CMOintment10");
                Interact(em, rig, ointmentFirst ? ointment : rig.Tool);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                var rows = Rows(em, rig.Part);
                Assert.That(rows.Single(row => row.Wound.Type == WoundType.Brute).Wound.Treated, Is.EqualTo(!ointmentFirst));
                Assert.That(rows.Single(row => row.Wound.Type == WoundType.Burn).Wound.Treated, Is.EqualTo(ointmentFirst));
                Assert.That(Damage(em, rig.Patient, "Slash"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Debt(em, rig.Part, "Slash"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Damage(em, rig.Patient, "Heat"), Is.EqualTo(FixedPoint2.New(ointmentFirst ? 5 : 10)));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(ointmentFirst ? 10 : 9));
                Assert.That(Count(em, ointment), Is.EqualTo(ointmentFirst ? 9 : 10));
                Interact(em, rig, ointmentFirst ? rig.Tool : ointment);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Rows(em, rig.Part).All(row => row.Wound.Treated), Is.True);
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.None));
                Assert.That(Damage(em, rig.Patient, "Slash"), Is.EqualTo(FixedPoint2.New(10)), "Dressing does not spend unrelated aggregate damage.");
                Assert.That(Damage(em, rig.Patient, "Heat"), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(9));
                Assert.That(Count(em, ointment), Is.EqualTo(9));
                Assert.That(em.HasComponent<CMUBandagePendingComponent>(rig.Medic), Is.False);
            });
        }
        finally { await pair.Server.WaitPost(() => { Delete(em, ointment); Delete(em, rig); }); }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArterialSourceRejectsOrdinaryGauzeAndAcceptsTheActualTraumaKit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        EntityUid trauma = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em);
                SetBleeding(em, rig.Part, ExternalBleedTier.Arterial);
                Interact(em, rig, rig.Tool);
                Assert.That(em.HasComponent<CMUBandagePendingComponent>(rig.Medic), Is.False);
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.Arterial));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(10));
                trauma = HeldTool(em, rig.Medic, "CMTraumaKit10");
                Interact(em, rig, trauma);
                // Medical skill 2 makes the shipped trauma kit immediate.
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.None));
                Assert.That(Count(em, trauma), Is.EqualTo(9));
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.None));
                Assert.That(Rows(em, rig.Part), Is.Empty);
                Assert.That(Count(em, trauma), Is.EqualTo(9));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(10));
                Assert.That(em.System<DamageableSystem>().GetTotalDamage(rig.Patient), Is.EqualTo(FixedPoint2.Zero));
            });
        }
        finally { await pair.Server.WaitPost(() => { Delete(em, trauma); Delete(em, rig); }); }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitBleedingSiteWinsBeforeSearchingForAnUnrelatedBruteWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        EntityUid other = default;
        CMUWoundEntry otherBefore = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                // One available dose isolates the first chosen effect from the
                // ordinary automatic repeat/search policy after a successful dose.
                rig = CreateRig(em, "CMGauze1");
                other = Arm(em, rig.Patient, BodyPartSymmetry.Left);
                Hit(em, rig.Patient, other, "Slash", 10);
                otherBefore = Rows(em, other).Single();
                SetBleeding(em, rig.Part, ExternalBleedTier.Moderate);
                Assert.That(Rows(em, rig.Part), Is.Empty);
                Assert.That(Debt(em, rig.Part, "Slash"), Is.EqualTo(FixedPoint2.Zero));
                Interact(em, rig, rig.Tool);
                AssertOperation(em, rig, rig.Tool);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.None));
                AssertUnchangedWoundBurden(Rows(em, other).Single(), otherBefore);
                Assert.That(Debt(em, other, "Slash"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Damage(em, rig.Patient, "Slash"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(em.EntityExists(rig.Tool), Is.False);
                Assert.That(em.HasComponent<CMUBandagePendingComponent>(rig.Medic), Is.False);
            });
        }
        finally { await pair.Server.WaitPost(() => Delete(em, rig)); }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancelledOrExhaustedBleedingTreatmentCannotCommitOrTreatTheBurn(bool exhaust)
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        CMUWoundEntry burnBefore = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em);
                Hit(em, rig.Patient, rig.Part, "Heat", 10);
                SetBleeding(em, rig.Part, ExternalBleedTier.Severe);
                burnBefore = Rows(em, rig.Part).Single();
                Interact(em, rig, rig.Tool);
                AssertOperation(em, rig, rig.Tool);
                if (exhaust)
                    Assert.That(em.System<SharedStackSystem>().TryUse(rig.Tool, 10), Is.True);
                else
                {
                    var operation = em.GetComponent<CMUBandagePendingComponent>(rig.Medic).Operations.Single();
                    em.System<SharedDoAfterSystem>().Cancel(operation.DoAfter.Id);
                }
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.Severe));
                AssertUnchangedWoundBurden(Rows(em, rig.Part).Single(), burnBefore);
                Assert.That(Damage(em, rig.Patient, "Heat"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Debt(em, rig.Part, "Heat"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(em.HasComponent<CMUBandagePendingComponent>(rig.Medic), Is.False);
                if (exhaust) Assert.That(em.EntityExists(rig.Tool), Is.False);
                else Assert.That(Count(em, rig.Tool), Is.EqualTo(10));
            });
        }
        finally { await pair.Server.WaitPost(() => Delete(em, rig)); }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreatingTheIndependentSourceStopsActualBloodDrain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        Rig rig = default;
        var before = 0f;
        var after = 0f;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, stasis: false);
                em.GetComponent<BloodstreamComponent>(rig.Patient).BloodRefreshAmount = FixedPoint2.Zero;
                em.System<BloodstreamSystem>().TrySetBleedAmount((rig.Patient, null), 0f);
                SetBleeding(em, rig.Part, ExternalBleedTier.Severe);
                before = em.System<BloodstreamSystem>().GetBloodLevel((rig.Patient, null));
            });
            await pair.RunSeconds(2.2f);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.System<BloodstreamSystem>().GetBloodLevel((rig.Patient, null)), Is.LessThan(before));
                Interact(em, rig, rig.Tool);
                AssertOperation(em, rig, rig.Tool);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Bleeding(em, rig.Part), Is.EqualTo(ExternalBleedTier.None));
                Assert.That(Count(em, rig.Tool), Is.EqualTo(9));
                after = em.System<BloodstreamSystem>().GetBloodLevel((rig.Patient, null));
            });
            await pair.RunSeconds(2.2f);
            await pair.Server.WaitAssertion(() => Assert.That(
                em.System<BloodstreamSystem>().GetBloodLevel((rig.Patient, null)), Is.EqualTo(after).Within(0.00001f)));
        }
        finally { await pair.Server.WaitPost(() => Delete(em, rig)); }
        await pair.CleanReturnAsync();
    }

    private static Rig CreateRig(IEntityManager em, string prototype = "CMGauze10", bool stasis = true)
    {
        var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var medic = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        if (stasis) em.AddComponent<CMInStasisComponent>(patient);
        em.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 2);
        em.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.RightArm);
        return new Rig(patient, medic, Arm(em, patient, BodyPartSymmetry.Right), HeldTool(em, medic, prototype));
    }

    private static EntityUid HeldTool(IEntityManager em, EntityUid medic, string prototype)
    {
        var tool = em.SpawnEntity(prototype, MapCoordinates.Nullspace);
        Assert.That(em.System<SharedHandsSystem>().TryPickupAnyHand(medic, tool), Is.True);
        return tool;
    }

    private static EntityUid Arm(IEntityManager em, EntityUid patient, BodyPartSymmetry side)
    {
        Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient, new(BodyPartType.Arm, side), out var part), Is.True);
        return part;
    }

    private static void SetBleeding(IEntityManager em, EntityUid part, ExternalBleedTier tier)
    {
        var wounds = em.EnsureComponent<BodyPartWoundComponent>(part);
        Assert.That(em.System<CMUWoundLedgerSystem>().TryUpdateExternalBleeding(part, tier, wounds), Is.True);
    }

    private static void Hit(IEntityManager em, EntityUid patient, EntityUid part, string type, int amount)
    {
        em.System<RegionalDamageProbeSystem>();
        em.EnsureComponent<RegionalDamageProbeComponent>(patient).Target = part;
        var damage = new DamageSpecifier { DamageDict = { [type] = amount } };
        var applied = em.System<DamageableSystem>().TryChangeDamage(patient, damage, ignoreResistances: true,
            impact: DamageImpact.ForContact(damage));
        Assert.That(applied!.GetTotal(), Is.EqualTo(FixedPoint2.New(amount)));
        Assert.That(Debt(em, part, type), Is.EqualTo(FixedPoint2.New(amount)));
    }

    private static void Interact(IEntityManager em, Rig rig, EntityUid tool)
    {
        var interact = new AfterInteractEvent(rig.Medic, tool, rig.Patient, default, true);
        em.EventBus.RaiseLocalEvent(tool, interact);
        Assert.That(interact.Handled, Is.True);
    }

    private static void AssertOperation(IEntityManager em, Rig rig, EntityUid tool)
    {
        Assert.That(em.TryGetComponent<CMUBandagePendingComponent>(rig.Medic, out var pending), Is.True,
            "The public item interaction must start its actual bleeding treatment.");
        var operation = pending!.Operations.Single();
        Assert.That(operation.Patient, Is.EqualTo(em.GetNetEntity(rig.Patient)));
        Assert.That(operation.Part, Is.EqualTo(em.GetNetEntity(rig.Part)));
        Assert.That(operation.Treater, Is.EqualTo(em.GetNetEntity(tool)));
    }

    private static void AssertUnchangedWoundBurden(CMUWoundEntry after, CMUWoundEntry before)
    {
        Assert.That(after.Wound.Type, Is.EqualTo(before.Wound.Type));
        Assert.That(after.Wound.Treated, Is.EqualTo(before.Wound.Treated));
        Assert.That(after.Wound.Damage, Is.EqualTo(before.Wound.Damage));
        Assert.That(after.Wound.Healed, Is.EqualTo(before.Wound.Healed));
        Assert.That(after.Size, Is.EqualTo(before.Size));
        Assert.That(after.Mechanism, Is.EqualTo(before.Mechanism));
        Assert.That(after.TreatmentQuality, Is.EqualTo(before.TreatmentQuality));
        Assert.That(after.Cleanup, Is.EqualTo(before.Cleanup));
    }

    private static IReadOnlyList<CMUWoundEntry> Rows(IEntityManager em, EntityUid part)
        => em.TryGetComponent<BodyPartWoundComponent>(part, out var wounds)
            ? em.System<CMUWoundLedgerSystem>().GetEntries(wounds) : Array.Empty<CMUWoundEntry>();

    private static ExternalBleedTier Bleeding(IEntityManager em, EntityUid part)
        => em.TryGetComponent<BodyPartWoundComponent>(part, out var wounds) ? wounds.ExternalBleeding : ExternalBleedTier.None;

    private static FixedPoint2 Damage(IEntityManager em, EntityUid patient, string type)
        => em.System<DamageableSystem>().GetAllDamage(patient).DamageDict.GetValueOrDefault(type);

    private static FixedPoint2 Debt(IEntityManager em, EntityUid part, string type)
        => em.System<SharedBodyPartHealthSystem>().GetAttributedDamage(part, type);

    private static int Count(IEntityManager em, EntityUid tool) => em.System<SharedStackSystem>().GetCount(tool);

    private static void Delete(IEntityManager em, Rig rig) => Delete(em, rig.Tool, rig.Patient, rig.Medic);

    private static void Delete(IEntityManager em, params EntityUid[] entities)
    {
        foreach (var entity in entities)
            if (em.EntityExists(entity)) em.DeleteEntity(entity);
    }

    private readonly record struct Rig(EntityUid Patient, EntityUid Medic, EntityUid Part, EntityUid Tool);
}
