using System.Collections.Generic;
using Content.Server._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Diagnostics.Examine;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Core;

[TestFixture]
public sealed class CMUMedicalChangeTest
{
    [Test]
    public async Task ChangesCoalesceAndAdvanceAggregateRevisionOncePerTick()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        CMUMedicalSnapshot initial = null;
        uint initialRevision = 0;
        uint changedRevision = 0;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var changes = entMan.System<CMUMedicalChangeSystem>();
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            _ = entMan.System<CMUMedicalChangeProbeSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            Assert.That(index.TryGetSnapshot(patient, out initial), Is.True);
            initialRevision = changes.GetRevision(patient);
        });

        // Flush spawn-time Anatomy before observing the mutation transaction under test.
        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var changes = entMan.System<CMUMedicalChangeSystem>();
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            entMan.AddComponent<CMUMedicalChangeProbeComponent>(patient);

            Assert.That(changes.MarkChanged(patient, CMUMedicalChangeFlags.Wounds), Is.True);
            changedRevision = changes.GetRevision(patient);
            Assert.That(changes.MarkChanged(patient, CMUMedicalChangeFlags.Pain), Is.True);
            Assert.That(index.TryGetSnapshot(patient, out var sameStructure), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(changedRevision, Is.EqualTo(initialRevision + 1));
                Assert.That(changes.GetRevision(patient), Is.EqualTo(changedRevision));
                Assert.That(sameStructure, Is.SameAs(initial));
            });
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalChangeProbeComponent>(patient);
            Assert.That(probe.Events, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(probe.Events[0].Revision, Is.EqualTo(changedRevision));
                Assert.That(
                    probe.Events[0].Changes,
                    Is.EqualTo(CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Pain));
            });
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task StructuralAndDynamicChangesShareOneRevisionAdvance(bool structureFirst)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid detachedArm = default;
        uint revisionAfterStructure = 0;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            _ = entMan.System<CMUMedicalChangeProbeSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        });

        // Flush spawn-time Anatomy before observing the transaction under test.
        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var changes = entMan.System<CMUMedicalChangeSystem>();
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            entMan.AddComponent<CMUMedicalChangeProbeComponent>(patient);

            Assert.That(index.TryGetBodyPart(
                patient,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out detachedArm), Is.True);

            if (structureFirst)
            {
                transform.DetachEntity(detachedArm, entMan.GetComponent<TransformComponent>(detachedArm));
                revisionAfterStructure = changes.GetRevision(patient);
                Assert.That(changes.MarkChanged(patient, CMUMedicalChangeFlags.Pain), Is.True);
            }
            else
            {
                Assert.That(changes.MarkChanged(patient, CMUMedicalChangeFlags.Pain), Is.True);
                revisionAfterStructure = changes.GetRevision(patient);
                transform.DetachEntity(detachedArm, entMan.GetComponent<TransformComponent>(detachedArm));
            }

            Assert.That(changes.GetRevision(patient), Is.EqualTo(revisionAfterStructure));
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalChangeProbeComponent>(patient);
            Assert.That(probe.Events, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(probe.Events[0].Revision, Is.EqualTo(revisionAfterStructure));
                Assert.That(
                    probe.Events[0].Changes,
                    Is.EqualTo(CMUMedicalChangeFlags.Anatomy | CMUMedicalChangeFlags.Pain));
            });

            if (entMan.EntityExists(detachedArm))
                entMan.DeleteEntity(detachedArm);
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectedDomainEventsFeedTheCoalescedJournal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            _ = entMan.System<CMUMedicalChangeProbeSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        });

        // Flush spawn-time Anatomy before observing the damage transaction.
        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            entMan.AddComponent<CMUMedicalChangeProbeComponent>(patient);

            Assert.That(index.TryGetBodyPart(
                patient,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out var arm), Is.True);

            var damage = new DamageSpecifier();
            damage.DamageDict["Heat"] = FixedPoint2.New(10);
            Assert.That(partHealth.TryApplyPartDamage(patient, arm, damage), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalChangeProbeComponent>(patient);
            Assert.That(probe.Events, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(probe.Events[0].Changes.HasFlag(CMUMedicalChangeFlags.Anatomy), Is.True);
                Assert.That(probe.Events[0].Changes.HasFlag(CMUMedicalChangeFlags.Wounds), Is.True);
            });
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InternalBleedingShrapnelAndEscharFeedTheCoalescedJournal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            _ = entMan.System<CMUMedicalChangeProbeSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        });

        // Flush spawn-time Anatomy before observing the mutation transaction.
        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var wounds = entMan.System<CMUWoundsSystem>();
            entMan.AddComponent<CMUMedicalChangeProbeComponent>(patient);

            Assert.That(index.TryGetBodyPart(
                patient,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out var arm), Is.True);

            wounds.SeedInternalBleed(arm, "test", 0.5f);
            Assert.That(shrapnel.AddShrapnel(arm, 1, 10f), Is.True);
            entMan.EnsureComponent<CMUEscharComponent>(arm);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalChangeProbeComponent>(patient);
            Assert.That(probe.Events, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(probe.Events[0].Changes.HasFlag(CMUMedicalChangeFlags.Wounds), Is.True);
                Assert.That(probe.Events[0].Changes.HasFlag(CMUMedicalChangeFlags.Surgery), Is.True);
            });
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultipleGranularEventsProduceOneCoalescedProjectionRefresh()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            _ = entMan.System<CMUMedicalChangeProbeSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        });

        // Build the empty spawn projection, then isolate the treatment transaction.
        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var projectionSystem = entMan.System<CMUMedicalExamineProjectionSystem>();
            var wounds = entMan.System<CMUWoundsSystem>();
            entMan.AddComponent<CMUMedicalChangeProbeComponent>(patient);

            Assert.That(index.TryGetBodyPart(
                patient,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None),
                out torso), Is.True);

            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(30);
            Assert.That(partHealth.TryApplyPartDamage(patient, torso, damage), Is.True);
            Assert.That(wounds.TryTreatWounds(torso, WoundType.Brute, 1, out var treated), Is.True);
            Assert.That(treated, Is.EqualTo(1));

            if (entMan.TryGetComponent<CMUMedicalExamineProjectionComponent>(patient, out var beforeFlush))
            {
                Assert.That(projectionSystem.TryGetPart(
                    beforeFlush,
                    BodyPartType.Torso,
                    BodyPartSymmetry.None,
                    out _), Is.False,
                    "Projection rebuilds must be driven by the coalesced body event, not each granular mutation.");
            }
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectionSystem = entMan.System<CMUMedicalExamineProjectionSystem>();
            var probe = entMan.GetComponent<CMUMedicalChangeProbeComponent>(patient);
            var projection = entMan.GetComponent<CMUMedicalExamineProjectionComponent>(patient);

            Assert.That(probe.Events, Has.Count.EqualTo(1));
            Assert.That(projectionSystem.TryGetPart(
                projection,
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                out var torsoProjection), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(probe.Events[0].Changes.HasFlag(CMUMedicalChangeFlags.Wounds), Is.True);
                Assert.That(probe.Events[0].Changes.HasFlag(CMUMedicalChangeFlags.Treatment), Is.True);
                Assert.That(torsoProjection.Wounds, Has.Count.EqualTo(1));
                Assert.That(torsoProjection.Wounds[0].Treated, Is.True);
            });
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }
}

[RegisterComponent]
public sealed partial class CMUMedicalChangeProbeComponent : Component
{
    public readonly List<CMUMedicalChangedEvent> Events = new();
}

public sealed class CMUMedicalChangeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUMedicalChangeProbeComponent, CMUMedicalChangedEvent>(OnMedicalChanged);
    }

    private void OnMedicalChanged(
        Entity<CMUMedicalChangeProbeComponent> ent,
        ref CMUMedicalChangedEvent args)
    {
        ent.Comp.Events.Add(args);
    }
}
