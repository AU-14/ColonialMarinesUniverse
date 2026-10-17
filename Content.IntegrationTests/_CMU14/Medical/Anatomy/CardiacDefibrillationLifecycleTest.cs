#pragma warning disable RA0002 // Assertions inspect cardiac state; probe callbacks use public mutation APIs.
using Content.Server.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class CardiacDefibrillationLifecycleTest
{
    private const string Reagent = "CMUCardiacRevivalElectrogenetic";
    private const string Prototypes = """
        - type: reagent
          id: CMUCardiacRevivalElectrogenetic
          name: cardiac revival test reagent
          desc: cardiac revival test reagent
          physicalDesc: reagent-physical-desc-translucent
          color: "#ffffff"
          worksOnTheDead: true
          metabolisms:
            Bloodstream:
              metabolismRate: 0.1
              effects:
              - !type:Electrogenetic
                potency: 2
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task RevivalWhileTheLayerIsDisabledDoesNotRestoreStaleArrestOnReenable(bool organOnly)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var cfg = pair.Server.CfgMan;
            var setting = organOnly ? CMUMedicalCCVars.OrganEnabled : CMUMedicalCCVars.Enabled;
            var original = cfg.GetCVar(setting);
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, heart, user, device) = Create(entities, coords);
            try
            {
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                var before = health.Current;
                cfg.SetCVar(setting, false);
                Zap(entities, device, patient, user);
                Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                Assert.That(health.Current, Is.EqualTo(before), "Disabled organ gameplay must retain its no-trauma policy.");
                cfg.SetCVar(setting, true);
                entities.System<SharedHeartSystem>().TickPulse(heart);
                Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.False);
                Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.False);
            }
            finally
            {
                cfg.SetCVar(setting, original);
                Delete(entities, patient, heart, user, device);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AContactCallbackDeletingTheDeviceStopsTheRemainingContactSnapshot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, heart, user, device) = Create(entities, coords);
            var contacts = new List<EntityUid>();
            var contactHearts = new Dictionary<EntityUid, FixedPoint2>();
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    var contact = entities.SpawnEntity("CMMobHuman", coords);
                    contacts.Add(contact);
                    entities.System<MobStateSystem>().ChangeMobState(contact, MobState.Dead);
                    Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(contact, out var contactHeart), Is.True);
                    contactHearts.Add(contactHeart, entities.GetComponent<OrganHealthComponent>(contactHeart).Current);
                    entities.EnsureComponent<CMUCardiacRevivalProbeComponent>(contact);
                }
                var patientProbe = entities.GetComponent<CMUCardiacRevivalProbeComponent>(patient);
                patientProbe.Contacts.AddRange(contacts);
                var deviceProbe = entities.GetComponent<CMUCardiacRevivalProbeComponent>(device);
                deviceProbe.DeleteOnContact = true;
                Zap(entities, device, patient, user);
                Assert.That(deviceProbe.Committed, Is.EqualTo(1), "The original patient completed before contact processing.");
                Assert.That(entities.EntityExists(device), Is.False);
                Assert.That(contacts.Sum(contact => entities.GetComponent<CMUCardiacRevivalProbeComponent>(contact).EligibilityAttempts), Is.EqualTo(1));
                foreach (var contact in contacts)
                    Assert.That(entities.GetComponent<MobStateComponent>(contact).CurrentState, Is.EqualTo(MobState.Dead));
                foreach (var (contactHeart, originalHealth) in contactHearts)
                {
                    Assert.That(entities.GetComponent<OrganHealthComponent>(contactHeart).Current, Is.EqualTo(originalHealth));
                    Assert.That(entities.GetComponent<HeartComponent>(contactHeart).Stopped, Is.True);
                }
            }
            finally
            {
                Delete(entities, patient, heart, user, device);
                Delete(entities, contacts.ToArray());
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PublicZapRevivesAndRestartsTheExactHealthyHeart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, heart, user, device) = Create(entities, coords);
            try
            {
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                var before = health.Current;
                Zap(entities, device, patient, user);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                    Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.False);
                    Assert.That(health.Current, Is.InRange(before - 5, before - 3));
                    Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.False);
                    Assert.That(entities.GetComponent<CMUCardiacRevivalProbeComponent>(device).Committed, Is.EqualTo(1));
                });
            }
            finally
            {
                Delete(entities, patient, heart, user, device);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(OrganDamageStage.Damaged)]
    [TestCase(OrganDamageStage.Failing)]
    [TestCase(OrganDamageStage.Dead)]
    [TestCase(null)]
    public async Task InvalidHeartCannotReviveEvenWhenBodyDamageIsAlreadyBelowDeathThreshold(OrganDamageStage? stage)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, heart, user, device) = Create(entities, coords);
            try
            {
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                if (stage is { } severity)
                {
                    var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[severity] } };
                    var ev = new OrganDamagedEvent(patient, heart, damage, OrganDamageSource.Direct);
                    entities.EventBus.RaiseLocalEvent(heart, ref ev);
                    Assert.That(health.Stage, Is.EqualTo(severity));
                }
                else
                    Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(heart), Is.True);
                var before = health.Current;
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(patient), Is.EqualTo(FixedPoint2.Zero));
                Zap(entities, device, patient, user);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Dead));
                    Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.True);
                    Assert.That(health.Current, Is.EqualTo(before), "Rejected eligibility cannot cause tissue trauma.");
                    Assert.That(entities.GetComponent<CMUCardiacRevivalProbeComponent>(device).Committed, Is.Zero);
                });
            }
            finally
            {
                Delete(entities, patient, heart, user, device);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ACompletedVetoPreventsTraumaHealingReagentConsumptionAndRevival(bool lateModifyVeto)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([Prototypes]);
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, heart, user, device) = Create(entities, coords);
            try
            {
                entities.System<DamageableSystem>().TryChangeDamage(patient,
                    new DamageSpecifier { DamageDict = { ["Poison"] = 20 } }, true);
                Assert.That(entities.System<SharedRMCBloodstreamSystem>().TryGetChemicalSolution(patient, out var solution, out _), Is.True);
                Assert.That(entities.System<SharedSolutionContainerSystem>().TryAddReagent(solution, Reagent, 2), Is.True);
                entities.GetComponent<CMUCardiacRevivalProbeComponent>(patient).CancelAttempt = !lateModifyVeto;
                entities.GetComponent<CMUCardiacRevivalProbeComponent>(device).CancelModify = lateModifyVeto;
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                var before = health.Current;
                Zap(entities, device, patient, user);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Dead));
                    Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.True);
                    Assert.That(health.Current, Is.EqualTo(before));
                    Assert.That(entities.System<DamageableSystem>().GetTotalDamage(patient), Is.EqualTo((FixedPoint2)20));
                    Assert.That(solution.Comp.Solution.GetTotalPrototypeQuantity(Reagent), Is.EqualTo((FixedPoint2)2));
                    Assert.That(entities.GetComponent<CMUCardiacRevivalProbeComponent>(device).Committed, Is.Zero);
                });
            }
            finally
            {
                Delete(entities, patient, heart, user, device);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("modify")]
    [TestCase("trauma")]
    [TestCase("commit")]
    [TestCase("health-replacement")]
    [TestCase("transplant")]
    [TestCase("delete")]
    [TestCase("queue-patient")]
    [TestCase("queue-heart")]
    [TestCase("queue-device")]
    [TestCase("device-delete")]
    [TestCase("metabolism")]
    public async Task CallbackChangesCannotTransferApprovalToMissingOrReplacementTissue(string boundary)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, heart, user, device) = Create(entities, coords);
            EntityUid? donor = null;
            EntityUid? replacement = null;
            FixedPoint2? replacementHealth = null;
            try
            {
                var probeEntity = boundary == "modify" ? device : boundary is "commit" or "metabolism" ? patient : heart;
                var probe = entities.GetComponent<CMUCardiacRevivalProbeComponent>(probeEntity);
                var deviceProbe = entities.GetComponent<CMUCardiacRevivalProbeComponent>(device);
                probe.Boundary = boundary;
                if (boundary == "transplant")
                {
                    donor = entities.SpawnEntity("CMMobHuman", coords);
                    entities.System<MobStateSystem>().ChangeMobState(donor.Value, MobState.Dead);
                    Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(donor.Value, out var donorHeart), Is.True);
                    replacement = donorHeart;
                    replacementHealth = entities.GetComponent<OrganHealthComponent>(donorHeart).Current;
                    Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(donorHeart), Is.True);
                    probe.ReplacementHeart = donorHeart;
                }
                Zap(entities, device, patient, user);
                if (boundary == "delete")
                {
                    Assert.That(entities.EntityExists(patient), Is.False);
                    Assert.That(probe.BoundaryCallbacks, Is.EqualTo(1));
                    Assert.That(deviceProbe.Committed, Is.Zero);
                    return;
                }
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Dead));
                    Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.True);
                    Assert.That(entities.GetComponent<CMUCardiacRevivalProbeComponent>(probeEntity).BoundaryCallbacks, Is.EqualTo(1));
                    Assert.That(deviceProbe.Committed, Is.Zero);
                });
                if (boundary is "queue-patient" or "queue-heart" or "queue-device" or "device-delete")
                    return;
                if (replacement is { } newHeart)
                {
                    Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out var current), Is.True);
                    Assert.That(current, Is.EqualTo(newHeart));
                    Assert.That(entities.GetComponent<HeartComponent>(newHeart).Stopped, Is.True, "An accepted attempt cannot restart a transplanted heart.");
                    Assert.That(entities.GetComponent<OrganHealthComponent>(newHeart).Current, Is.EqualTo(replacementHealth));
                }
                else if (boundary == "health-replacement")
                    Assert.That(entities.GetComponent<OrganHealthComponent>(heart).Current, Is.EqualTo((FixedPoint2)50));
                else
                {
                    Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out _), Is.False);
                    Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.True);
                }
            }
            finally
            {
                Delete(entities, patient, heart, user, device);
                if (donor is { } donorBody)
                    Delete(entities, donorBody);
                if (replacement is { } donorHeart)
                    Delete(entities, donorHeart);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NestedZapDuringTraumaCannotDuplicateCardiacEffectsOrCommittedRevival()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([Prototypes]);
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, heart, user, device) = Create(entities, coords);
            var secondDevice = entities.SpawnEntity("CMDefibrillator", coords);
            try
            {
                entities.EnsureComponent<CMUCardiacRevivalProbeComponent>(secondDevice);
                Assert.That(entities.System<ItemToggleSystem>().TryActivate(secondDevice, user: user), Is.True);
                Assert.That(entities.System<SharedRMCBloodstreamSystem>().TryGetChemicalSolution(patient, out var solution, out _), Is.True);
                Assert.That(entities.System<SharedSolutionContainerSystem>().TryAddReagent(solution, Reagent, 2), Is.True);
                var probe = entities.GetComponent<CMUCardiacRevivalProbeComponent>(heart);
                probe.NestedDevice = secondDevice;
                probe.User = user;
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                var before = health.Current;
                Zap(entities, device, patient, user);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.TraumaEvents, Is.EqualTo(1));
                    Assert.That(health.Current, Is.InRange(before - 5, before - 3));
                    Assert.That(solution.Comp.Solution.GetTotalPrototypeQuantity(Reagent), Is.EqualTo((FixedPoint2)1));
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                    Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.False);
                    Assert.That(entities.GetComponent<CMUCardiacRevivalProbeComponent>(device).Committed, Is.EqualTo(1));
                    Assert.That(entities.GetComponent<CMUCardiacRevivalProbeComponent>(secondDevice).Committed, Is.Zero);
                });
            }
            finally
            {
                Delete(entities, patient, heart, user, device, secondDevice);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Patient, EntityUid Heart, EntityUid User, EntityUid Device) Create(IEntityManager entities, MapCoordinates coords)
    {
        _ = entities.System<CMUCardiacRevivalProbeSystem>();
        var patient = entities.SpawnEntity("CMMobHuman", coords);
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out var heart), Is.True);
        var user = entities.SpawnEntity("MobHuman", coords);
        var device = entities.SpawnEntity("CMDefibrillator", coords);
        entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
        foreach (var entity in new[] { patient, heart, device })
        {
            var probe = entities.EnsureComponent<CMUCardiacRevivalProbeComponent>(entity);
            probe.Patient = patient;
            probe.Heart = heart;
            probe.Device = device;
        }
        Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.True);
        Assert.That(entities.System<ItemToggleSystem>().TryActivate(device, user: user), Is.True);
        return (patient, heart, user, device);
    }

    private static void Zap(IEntityManager entities, EntityUid device, EntityUid patient, EntityUid user)
    {
        var defibs = entities.System<SharedDefibrillatorSystem>();
        Assert.That(defibs.CanZap(device, patient, user), Is.True);
        defibs.Zap(device, patient, user);
    }

    private static void Delete(IEntityManager entities, params EntityUid[] targets)
    {
        foreach (var target in targets)
        {
            if (entities.EntityExists(target))
                entities.DeleteEntity(target);
        }
    }
}

[RegisterComponent]
public sealed partial class CMUCardiacRevivalProbeComponent : Component
{
    public EntityUid Patient;
    public EntityUid Heart;
    public EntityUid User;
    public EntityUid Device;
    public EntityUid? NestedDevice;
    public EntityUid? ReplacementHeart;
    public bool CancelAttempt;
    public bool CancelModify;
    public bool DeleteOnContact;
    public readonly List<EntityUid> Contacts = new();
    public string? Boundary;
    public int BoundaryCallbacks;
    public int Committed;
    public int TraumaEvents;
    public int EligibilityAttempts;
}

public sealed class CMUCardiacRevivalProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCardiacRevivalProbeComponent, RMCDefibrillatorAttemptEvent>(OnAttempt,
            after: [typeof(HeartDefibrillatorPatchSystem)]);
        SubscribeLocalEvent<CMUCardiacRevivalProbeComponent, RMCDefibrillatorDamageModifyEvent>(OnModify,
            after: [typeof(RMCDefibrillatorSystem)]);
        SubscribeLocalEvent<CMUCardiacRevivalProbeComponent, RMCDefibrillatorRevivedEvent>(OnRevived);
        SubscribeLocalEvent<CMUCardiacRevivalProbeComponent, OrganDamagedEvent>(OnTrauma,
            after: [typeof(SharedOrganHealthSystem)]);
        SubscribeLocalEvent<CMUCardiacRevivalProbeComponent, MobStateChangedEvent>(OnMobChanged);
        SubscribeLocalEvent<CMUCardiacRevivalProbeComponent, CMMetabolizeAttemptEvent>(OnMetabolize);
        SubscribeLocalEvent<CMUCardiacRevivalProbeComponent, GetInteractingEntitiesEvent>(OnContacts);
    }

    private void OnAttempt(Entity<CMUCardiacRevivalProbeComponent> ent, ref RMCDefibrillatorAttemptEvent args)
    {
        ent.Comp.EligibilityAttempts++;
        if (ent.Comp.CancelAttempt)
            args.Cancel();
    }

    private void OnModify(Entity<CMUCardiacRevivalProbeComponent> ent, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (ent.Comp.DeleteOnContact && args.Target != ent.Comp.Patient)
        {
            Del(ent.Owner);
            return;
        }
        if (ent.Comp.CancelModify)
            args.Cancelled = true;
        ChangeTissue(ent, "modify");
    }

    private void OnTrauma(Entity<CMUCardiacRevivalProbeComponent> ent, ref OrganDamagedEvent args)
    {
        ent.Comp.TraumaEvents++;
        ChangeTissue(ent, "trauma");
        if (ent.Comp.Boundary is "queue-patient" or "queue-heart" or "queue-device" or "device-delete")
        {
            var boundary = ent.Comp.Boundary;
            ent.Comp.Boundary = null;
            ent.Comp.BoundaryCallbacks++;
            if (boundary == "device-delete")
                Del(ent.Comp.Device);
            else
                QueueDel(boundary == "queue-patient" ? ent.Comp.Patient : boundary == "queue-heart" ? ent.Comp.Heart : ent.Comp.Device);
            return;
        }
        if (ent.Comp.Boundary == "delete")
        {
            ent.Comp.Boundary = null;
            ent.Comp.BoundaryCallbacks++;
            Del(ent.Comp.Patient);
            return;
        }
        if (ent.Comp.Boundary == "transplant" && ent.Comp.ReplacementHeart is { } replacement)
        {
            ent.Comp.Boundary = null;
            ent.Comp.BoundaryCallbacks++;
            var index = EntityManager.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrganPart(ent.Comp.Heart, out var part), Is.True);
            var slot = index.GetOrganSlots(part).Single(entry => entry.Organ == ent.Comp.Heart).SlotId;
            var bodies = EntityManager.System<SharedBodySystem>();
            Assert.That(bodies.RemoveOrgan(ent.Comp.Heart), Is.True);
            Assert.That(bodies.InsertOrgan(part, replacement, slot), Is.True);
        }
        if (ent.Comp.Boundary == "health-replacement")
        {
            ent.Comp.Boundary = null;
            ent.Comp.BoundaryCallbacks++;
            RemComp<OrganHealthComponent>(ent.Comp.Heart);
            EnsureComp<OrganHealthComponent>(ent.Comp.Heart);
        }
        if (ent.Comp.NestedDevice is { } device)
        {
            ent.Comp.NestedDevice = null;
            EntityManager.System<SharedDefibrillatorSystem>().Zap(device, ent.Comp.Patient, ent.Comp.User);
        }
    }

    private void OnMobChanged(Entity<CMUCardiacRevivalProbeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
            ChangeTissue(ent, "commit");
    }

    private static void OnRevived(Entity<CMUCardiacRevivalProbeComponent> ent, ref RMCDefibrillatorRevivedEvent args)
        => ent.Comp.Committed++;

    private static void OnContacts(Entity<CMUCardiacRevivalProbeComponent> ent, ref GetInteractingEntitiesEvent args)
        => args.InteractingEntities.UnionWith(ent.Comp.Contacts);

    private void OnMetabolize(Entity<CMUCardiacRevivalProbeComponent> ent, ref CMMetabolizeAttemptEvent args)
    {
        if (TryComp<MobStateComponent>(ent.Owner, out var mob) && mob.CurrentState != MobState.Dead)
            ChangeTissue(ent, "metabolism");
    }

    private void ChangeTissue(Entity<CMUCardiacRevivalProbeComponent> ent, string boundary)
    {
        if (ent.Comp.Boundary != boundary)
            return;
        ent.Comp.Boundary = null;
        ent.Comp.BoundaryCallbacks++;
        Assert.That(EntityManager.System<SharedBodySystem>().RemoveOrgan(ent.Comp.Heart), Is.True);
    }
}
