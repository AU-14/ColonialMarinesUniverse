using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Explosion;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Surgery.Markers;
using Content.Server._CMU14.Medical.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Eye;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class RMCHumanPrototypeRegressionTest
{
    [Test]
    public async Task CMMobHumanHasExpectedBuis()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<SharedUserInterfaceSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ui.HasUi(human, TacticalMapUserUi.Key), Is.True);
                    Assert.That(ui.HasUi(human, CMUSurgeryUIKey.Key), Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MobHumanDummyUsesCmuMedicalBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dummy = entMan.SpawnEntity("MobHumanDummy", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUHumanMedicalComponent>(dummy), Is.True);
                    Assert.That(entMan.HasComponent<CMSurgeryTargetComponent>(dummy), Is.True);
                    Assert.That(entMan.GetComponent<BodyComponent>(dummy).Prototype?.Id, Is.EqualTo("CMUHumanBody"));
                });
            }
            finally
            {
                entMan.DeleteEntity(dummy);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuAttachedInternalsUsePrivateVisibilityLayer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var internalLayer = (ushort) VisibilityFlags.CMUMedicalInternals;

            try
            {
                var checkedParts = 0;
                foreach (var (partUid, _) in body.GetBodyChildren(human))
                {
                    checkedParts++;
                    var visibility = entMan.GetComponent<VisibilityComponent>(partUid);
                    Assert.That(visibility.Layer & internalLayer, Is.EqualTo(internalLayer));
                }

                var checkedOrgans = 0;
                foreach (var organ in body.GetBodyOrgans(human))
                {
                    checkedOrgans++;
                    var visibility = entMan.GetComponent<VisibilityComponent>(organ.Id);
                    Assert.That(visibility.Layer & internalLayer, Is.EqualTo(internalLayer));
                }

                Assert.Multiple(() =>
                {
                    Assert.That(checkedParts, Is.GreaterThan(0));
                    Assert.That(checkedOrgans, Is.GreaterThan(0));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuDetachedOrgansLeavePrivateVisibilityLayer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid organ = default;
        var internalLayer = (ushort) VisibilityFlags.CMUMedicalInternals;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            foreach (var bodyOrgan in body.GetBodyOrgans(human))
            {
                organ = bodyOrgan.Id;
                break;
            }

            Assert.That(organ, Is.Not.EqualTo(default(EntityUid)));

            var visibility = entMan.GetComponent<VisibilityComponent>(organ);
            Assert.That(visibility.Layer & internalLayer, Is.EqualTo(internalLayer));

            Assert.That(body.RemoveOrgan(organ), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var visibility = entMan.GetComponent<VisibilityComponent>(organ);
            Assert.That(visibility.Layer & internalLayer, Is.EqualTo(0));

            entMan.DeleteEntity(organ);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSynthMissingLimbShowsSynthReattachSurgery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var xform = entMan.System<SharedTransformSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<SynthComponent>(patient);
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                xform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));

                var entries = dispatch.BuildPartEntries(patient, surgeon);
                var leftArmEntry = entries.Find(entry =>
                    entry.Type == BodyPartType.Arm &&
                    entry.Symmetry == BodyPartSymmetry.Left);

                Assert.That(leftArmEntry, Is.Not.Null);

                var surgeryIds = leftArmEntry!.EligibleSurgeries.ConvertAll(entry => entry.SurgeryId);
                Assert.Multiple(() =>
                {
                    Assert.That(surgeryIds, Does.Contain("RMCSynthSurgeryReattachLimb"));
                    Assert.That(surgeryIds, Does.Not.Contain("CMUSurgeryReattachLimb"));
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSurgeryToolUseOnOtherSurgeonInFlightOpensUiInsteadOfAutoResuming()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var originalSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var newSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(originalSurgeon, "RMCSkillSurgery", 3);
                skills.SetSkill(newSurgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                entMan.EnsureComponent<InternalBleedingComponent>(leftArm);
                flow.EnsureSurgeryInFlight(
                    patient,
                    leftArm,
                    originalSurgeon,
                    "CMUSurgeryCauterizeInternalBleeding",
                    flow.ResolveSurgeryDisplayName("CMUSurgeryCauterizeInternalBleeding"),
                    BodyPartType.Arm,
                    BodyPartSymmetry.Left);

                Assert.That(dispatch.TryDispatch(newSurgeon, patient, scalpel), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(newSurgeon), Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
                    Assert.That(entMan.GetComponent<CMUSurgeryInFlightComponent>(leftArm).Surgeon, Is.EqualTo(originalSurgeon));
                });

                var originalState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, originalSurgeon),
                    null,
                    originalSurgeon);
                var newState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, newSurgeon),
                    null,
                    newSurgeon);

                Assert.Multiple(() =>
                {
                    Assert.That(originalState.InFlight, Is.Not.Null);
                    Assert.That(originalState.InFlight!.OwnedByViewer, Is.True);
                    Assert.That(newState.InFlight, Is.Not.Null);
                    Assert.That(newState.InFlight!.OwnedByViewer, Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(originalSurgeon);
                entMan.DeleteEntity(newSurgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSurgeryToolUseByOtherSurgeonExposesArmedStepForTakeover()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var originalSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var newSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(originalSurgeon, "RMCSkillSurgery", 3);
                skills.SetSkill(newSurgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                entMan.EnsureComponent<InternalBleedingComponent>(leftArm);

                var armed = flow.TryArmStep(
                    originalSurgeon,
                    patient,
                    leftArm,
                    "CMUSurgeryCauterizeInternalBleeding",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Left);

                Assert.That(armed, Is.Not.Null);
                Assert.That(dispatch.TryDispatch(newSurgeon, patient, scalpel), Is.True);

                var currentArmed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
                var originalState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, originalSurgeon),
                    currentArmed,
                    originalSurgeon);
                var newState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, newSurgeon),
                    currentArmed,
                    newSurgeon);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(newSurgeon), Is.True);
                    Assert.That(currentArmed.Surgeon, Is.EqualTo(originalSurgeon));
                    Assert.That(originalState.CurrentArmedStep, Is.Not.Null);
                    Assert.That(newState.CurrentArmedStep, Is.Not.Null);
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(originalSurgeon);
                entMan.DeleteEntity(newSurgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuOwningSurgeonCanReopenMenuWithScalpelWhenAnotherToolIsArmed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                entMan.EnsureComponent<CMIncisionOpenComponent>(leftArm);
                entMan.EnsureComponent<CMBleedersClampedComponent>(leftArm);
                entMan.EnsureComponent<CMSkinRetractedComponent>(leftArm);
                entMan.EnsureComponent<InternalBleedingComponent>(leftArm);

                var armed = flow.TryArmStep(
                    surgeon,
                    patient,
                    leftArm,
                    "CMUSurgeryCauterizeInternalBleeding",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Left);

                Assert.That(armed, Is.Not.Null);
                Assert.That(armed!.RequiredToolCategory, Is.EqualTo("hemostat"));
                Assert.That(
                    flow.TryHandleArmedToolUse(
                        patient,
                        armed,
                        surgeon,
                        scalpel,
                        leftArm,
                        out var handled,
                        out var started),
                    Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(handled, Is.False);
                    Assert.That(started, Is.False);
                    Assert.That(entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient).Surgeon, Is.EqualTo(surgeon));
                });

                Assert.That(dispatch.TryDispatch(surgeon, patient, scalpel), Is.True);

                var currentArmed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
                var state = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, surgeon),
                    currentArmed,
                    surgeon);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.True);
                    Assert.That(currentArmed.Surgeon, Is.EqualTo(surgeon));
                    Assert.That(state.CurrentArmedStep, Is.Not.Null);
                    Assert.That(state.CurrentArmedStep!.ToolCategory, Is.EqualTo("hemostat"));
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuFreshSurgeryToolUseWithSelectedPartOpensUiInsteadOfAutoStarting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);
                targeting.SelectZone((surgeon, null), TargetBodyZone.LeftArm);

                Assert.That(dispatch.TryDispatch(surgeon, patient, scalpel), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(patient), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuLimbReattachKeepsCloseUpOnAttachedLimb()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid leftArm = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var xform = entMan.System<SharedTransformSystem>();

            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
            standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

            foreach (var (partUid, part) in body.GetBodyChildren(patient))
            {
                if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                    continue;

                leftArm = partUid;
                break;
            }

            Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));

            xform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));
            Assert.That(hands.TryPickupAnyHand(surgeon, leftArm, checkActionBlocker: false), Is.True);

            entMan.EnsureComponent<CMIncisionOpenComponent>(patient);
            entMan.EnsureComponent<CMBleedersClampedComponent>(patient);
            entMan.EnsureComponent<CMSkinRetractedComponent>(patient);
            entMan.EnsureComponent<CMUStumpRemovedComponent>(patient);
            entMan.EnsureComponent<CMUReattachPreppedComponent>(patient);

            var armed = flow.TryArmStep(
                surgeon,
                patient,
                patient,
                "CMUSurgeryReattachLimb",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Left);

            Assert.That(armed, Is.Not.Null);
            Assert.That(armed!.RequiredToolCategory, Is.EqualTo("severed_limb"));
            Assert.That(flow.TryHandleArmedToolUse(patient, armed, surgeon, leftArm, patient, out var handled, out var started), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(started, Is.True);
            });
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var part = entMan.GetComponent<BodyPartComponent>(leftArm);
            var lockComp = entMan.GetComponent<CMUSurgeryInProgressComponent>(patient);
            var entries = dispatch.BuildPartEntries(patient, surgeon);
            var leftArmEntry = entries.Find(entry =>
                entry.Type == BodyPartType.Arm &&
                entry.Symmetry == BodyPartSymmetry.Left);

            Assert.Multiple(() =>
            {
                Assert.That(part.Body, Is.EqualTo(patient));
                Assert.That(lockComp.Part, Is.EqualTo(leftArm));
                Assert.That(lockComp.AwaitingClosureChoice, Is.True);
                Assert.That(entMan.HasComponent<CMUSurgeryInFlightComponent>(leftArm), Is.True);
                Assert.That(entMan.HasComponent<CMUReattachCompleteComponent>(leftArm), Is.True);
                Assert.That(entMan.HasComponent<CMUReattachCompleteComponent>(patient), Is.False);
                Assert.That(leftArmEntry, Is.Not.Null);
            });

            Assert.That(leftArmEntry!.EligibleSurgeries, Has.Some.Matches<CMUSurgeryEntry>(entry =>
                entry.SurgeryId == "CMUSurgeryReattachLimb" &&
                entry.Category == "close_up" &&
                entry.NextStepToolCategory == "cautery"));

            entMan.DeleteEntity(patient);
            entMan.DeleteEntity(surgeon);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RMCExplosionPrototypesKeepBaseDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();
        var proto = server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            AssertExplosionDamage(proto, "RMC", 5f, 5f);
            AssertExplosionDamage(proto, "RMCMortar", 6.25f, 6.25f);
            AssertExplosionDamage(proto, "RMCOB", 5f, 5f);
            AssertExplosionDamage(proto, "RMCOBXenoTunnel", 5f, 5f);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RMCExplosionCreatesCmuBlastWoundsOnMultipleParts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var damage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = FixedPoint2.New(50),
                        ["Heat"] = FixedPoint2.New(50),
                    },
                };

                var explosion = new ExplosionReceivedEvent("RMC", MapCoordinates.Nullspace, damage);
                entMan.EventBus.RaiseLocalEvent(human, ref explosion);

                var woundedParts = 0;
                foreach (var (partUid, _) in body.GetBodyChildren(human))
                {
                    if (entMan.TryGetComponent<BodyPartWoundComponent>(partUid, out var wounds) &&
                        wounds.Wounds.Count > 0)
                    {
                        woundedParts++;
                    }
                }

                Assert.That(woundedParts, Is.GreaterThanOrEqualTo(3));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HumanAndSynthExplosionResistanceAppliesVulnerability()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var synth = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var humanSynth = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var other = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<SynthComponent>(synth);
                entMan.EnsureComponent<SynthComponent>(humanSynth);

                Assert.Multiple(() =>
                {
                    AssertExplosionCoefficient(entMan, human, 2.25f, "CMU human");
                    AssertExplosionCoefficient(entMan, synth, 2.25f, "Synth");
                    AssertExplosionCoefficient(entMan, humanSynth, 2.25f, "CMU synth");
                    AssertExplosionCoefficient(entMan, other, 1f, "Unmarked entity");
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(synth);
                entMan.DeleteEntity(humanSynth);
                entMan.DeleteEntity(other);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertExplosionDamage(IPrototypeManager proto, string id, float blunt, float heat)
    {
        var explosion = proto.Index<ExplosionPrototype>(id);
        Assert.That(explosion.DamagePerIntensity.DamageDict["Blunt"], Is.EqualTo((FixedPoint2) blunt), $"{id} Blunt damage");
        Assert.That(explosion.DamagePerIntensity.DamageDict["Heat"], Is.EqualTo((FixedPoint2) heat), $"{id} Heat damage");
    }

    private static void AssertExplosionCoefficient(IEntityManager entMan, EntityUid entity, float expected, string message)
    {
        var ev = new GetExplosionResistanceEvent("RMC");
        entMan.EventBus.RaiseLocalEvent(entity, ref ev);

        Assert.That(ev.DamageCoefficient, Is.EqualTo(expected).Within(0.001f), message);
    }
}
