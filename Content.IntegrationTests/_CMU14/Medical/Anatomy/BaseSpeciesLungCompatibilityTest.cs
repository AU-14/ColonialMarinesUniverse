#pragma warning disable RA0002 // Observe public respiration's committed gas and movement state.
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.Movement.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class BaseSpeciesLungCompatibilityTest
{
    [TestCase("CMMobSlimePerson")]
    [TestCase("CMMobArachnid")]
    public async Task BaseSpeciesLungsBreatheAndOnlyTheirActualRemovalReducesCapacity(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid lung = default;
        EntityUid site = default;
        float normalWalk = 0;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<CMUInhaleGasProbeSystem>();
                patient = entities.SpawnEntity(prototype, map.GridCoords);
                entities.AddComponent<CMUInhaleGasProbeComponent>(patient);
                Assert.That(entities.HasComponent<CMUHumanMedicalComponent>(patient), Is.True);
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetOrgan<LungComponent>(patient, out lung), Is.True);
                Assert.That(index.TryGetOrganPart(lung, out site), Is.True);
                Assert.That(entities.HasComponent<LungsComponent>(lung), Is.False,
                    "Exercise the real base species organ, not a CMU human lung replacement.");
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                AssertHealthyCapacity(entities, patient);
                normalWalk = entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed;
                Assert.That(normalWalk, Is.GreaterThan(0));
                AssertInhale(entities, patient, lung);
                Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(lung), Is.True);
                Assert.That(entities.System<SharedLungsSystem>().TryGetRespiratoryCapacity(patient, out _), Is.False);
                AssertInhale(entities, patient, null);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<MissingLungsComponent>(patient), Is.True);
                Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUPulmonaryEdema"), Is.True);
                Assert.That(entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed,
                    Is.EqualTo(normalWalk * 0.85f).Within(0.0001f));
                Assert.That(entities.System<SharedBodySystem>().InsertOrgan(site, lung, "lungs"), Is.True);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                AssertHealthyCapacity(entities, patient);
                Assert.That(entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed,
                    Is.EqualTo(normalWalk).Within(0.0001f));
                AssertInhale(entities, patient, lung);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
                if (entities.EntityExists(lung)) entities.DeleteEntity(lung);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BaseLungTransplantClearsMissingHumanCapacityAndEdema()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid donorBody = default;
        EntityUid removed = default;
        EntityUid donor = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<CMUInhaleGasProbeSystem>();
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                donorBody = entities.SpawnEntity("CMMobSlimePerson", map.GridCoords);
                entities.AddComponent<CMUInhaleGasProbeComponent>(patient);
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                var body = entities.System<SharedBodySystem>();
                Assert.That(index.TryGetOrgan<LungsComponent>(patient, out removed), Is.True);
                Assert.That(index.TryGetOrganPart(removed, out var site), Is.True);
                Assert.That(index.TryGetOrgan<LungComponent>(donorBody, out donor), Is.True);
                Assert.That(body.RemoveOrgan(removed), Is.True);
                Assert.That(entities.HasComponent<MissingLungsComponent>(patient), Is.True);
                Assert.That(body.RemoveOrgan(donor), Is.True);
                Assert.That(body.InsertOrgan(site, donor, "lungs"), Is.True);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                AssertHealthyCapacity(entities, patient);
                AssertInhale(entities, patient, donor);
                Assert.That(entities.HasComponent<MissingLungsComponent>(donorBody), Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
                if (entities.EntityExists(donorBody)) entities.DeleteEntity(donorBody);
                if (entities.EntityExists(removed)) entities.DeleteEntity(removed);
                if (entities.EntityExists(donor)) entities.DeleteEntity(donor);
            });
        }
        await pair.CleanReturnAsync();
    }

    private static void AssertHealthyCapacity(IEntityManager entities, EntityUid patient)
    {
        Assert.That(entities.System<SharedLungsSystem>().TryGetRespiratoryCapacity(patient, out var capacity), Is.True);
        Assert.That(capacity.Efficiency, Is.EqualTo(1f));
        Assert.That(capacity.Stage, Is.EqualTo(OrganDamageStage.Healthy));
        Assert.That(entities.HasComponent<MissingLungsComponent>(patient), Is.False);
        Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUPulmonaryEdema"), Is.False);
        Assert.That(entities.System<SharedCMUMedicalSpeedSystem>().ComputeMovementMultiplier(patient), Is.EqualTo(1f));
    }

    private static void AssertInhale(IEntityManager entities, EntityUid patient, EntityUid? lung)
    {
        var probe = entities.GetComponent<CMUInhaleGasProbeComponent>(patient);
        // One mole per liter makes the real inhaled amount equal the configured breath volume.
        probe.Source = new GasMixture(100);
        probe.Source.SetMoles(Gas.Oxygen, 100);
        probe.LastInhaledMoles = -1;
        // RMC humans and these species have lungs but no active base Respirator.
        // Explicitly enable the optional adapter for this public API call only;
        // do not change species gameplay respiration just to exercise the bridge.
        var addedRespirator = !entities.HasComponent<RespiratorComponent>(patient);
        var respirator = entities.EnsureComponent<RespiratorComponent>(patient);
        try
        {
            var airBefore = lung is { } original ? entities.GetComponent<LungComponent>(original).Air.TotalMoles : 0;
            entities.System<RespiratorSystem>().Inhale((patient, respirator));
            var expected = lung is null ? 0 : respirator.BreathVolume;
            Assert.That(probe.LastInhaledMoles, Is.EqualTo(expected).Within(0.0001f),
                "The actual inhale callback must run, including zero-volume inhalation without lungs.");
            if (lung is { } attached)
                Assert.That(entities.GetComponent<LungComponent>(attached).Air.TotalMoles,
                    Is.EqualTo(airBefore + expected).Within(0.0001f));
        }
        finally
        {
            if (addedRespirator)
                entities.RemoveComponent<RespiratorComponent>(patient);
        }
    }
}

[RegisterComponent]
public sealed partial class CMUInhaleGasProbeComponent : Component
{
    public GasMixture? Source;
    public float LastInhaledMoles;
}

public sealed partial class CMUInhaleGasProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUInhaleGasProbeComponent, InhaleLocationEvent>(OnLocation);
        SubscribeLocalEvent<CMUInhaleGasProbeComponent, InhaledGasEvent>(OnInhaled,
            before: new[] { typeof(RespiratorSystem) });
    }

    private void OnLocation(Entity<CMUInhaleGasProbeComponent> ent, ref InhaleLocationEvent args)
    {
        if (ent.Comp.Source is { } source)
            args.Gas = source;
    }

    private void OnInhaled(Entity<CMUInhaleGasProbeComponent> ent, ref InhaledGasEvent args)
    {
        ent.Comp.LastInhaledMoles = args.Gas.TotalMoles;
    }
}
