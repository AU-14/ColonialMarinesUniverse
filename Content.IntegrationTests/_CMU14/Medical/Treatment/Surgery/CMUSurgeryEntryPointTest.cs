using System.Linq;
using System.Reflection;
using Content.Client._CMU14.Medical.Treatment.Surgery;
using Content.IntegrationTests.Pair;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Server.Mind;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Part;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using ServerBodyZoneTargetingSystem = Content.Server._CMU14.Medical.Anatomy.BodyParts.BodyZoneTargetingSystem;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUSurgeryEntryPointTest
{
    [Test]
    public async Task HandsAndFeetOfferExtremitySurgeries()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid surgeon = default;
        EntityUid patient = default;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            surgeon = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            PreparePatient(entities, surgeon, patient);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            var rulebook = entities.System<CMUSurgeryRulebookSystem>();
            var transform = entities.System<SharedTransformSystem>();

            foreach (var type in new[] { BodyPartType.Hand, BodyPartType.Foot })
            {
                Assert.That(
                    index.TryGetBodyPart(
                        patient,
                        new CMUMedicalBodyPartKey(type, BodyPartSymmetry.Left),
                        out var part),
                    Is.True);

                entities.EnsureComponent<InternalBleedingComponent>(part);
                var attachedEntries = rulebook.BuildEligibleSurgeries(
                    patient,
                    type,
                    BodyPartSymmetry.Left,
                    surgeon,
                    part);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        attachedEntries.Select(entry => entry.SurgeryId),
                        Does.Contain("CMUSurgeryCauterizeInternalBleeding"));
                    Assert.That(
                        attachedEntries.Select(entry => entry.SurgeryId),
                        Does.Contain("CMUSurgeryRemoveLimb"));
                });

                transform.DetachEntity(part, entities.GetComponent<TransformComponent>(part));

                var missingEntry = rulebook.BuildPartEntries(patient, surgeon)
                    .Single(entry => entry.Type == type && entry.Symmetry == BodyPartSymmetry.Left);
                Assert.That(
                    missingEntry.EligibleSurgeries.Select(entry => entry.SurgeryId),
                    Does.Contain("CMUSurgeryReattachLimb"));
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgeryToolOpensCmuSurgeryWindow()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            // Avoid pooled map cleanup networking a humanoid body-tree deletion through the client fixture.
            Destructive = true,
            Fresh = true,
        });

        try
        {
            var server = pair.Server;
            var client = pair.Client;
            var session = server.PlayerMan.Sessions.Single();
            EntityUid surgeon = default;
            EntityUid patient = default;
            EntityUid scalpel = default;
            NetEntity surgeonNet = default;

            await server.WaitPost(() =>
            {
                var entities = server.EntMan;
                surgeon = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                scalpel = entities.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);
                surgeonNet = entities.GetNetEntity(surgeon);

                var mind = entities.System<MindSystem>();
                var mindId = mind.CreateMind(session.UserId, "CMU surgery test surgeon");
                mind.TransferTo(mindId, surgeon);
                mind.SetUserId(mindId, session.UserId);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var entities = server.EntMan;
                PreparePatient(entities, surgeon, patient);

                var interact = new AfterInteractEvent(
                    surgeon,
                    scalpel,
                    patient,
                    entities.GetComponent<TransformComponent>(patient).Coordinates,
                    canReach: true);
                entities.EventBus.RaiseLocalEvent(scalpel, interact);

                var ui = entities.System<SharedUserInterfaceSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(surgeon, CMUSurgeryUIKey.Key, surgeon), Is.True);
                    Assert.That(
                        ui.TryGetUiState<CMUSurgeryBuiState>(surgeon, CMUSurgeryUIKey.Key, out var state),
                        Is.True);
                    Assert.That(state!.Patient, Is.EqualTo(entities.GetNetEntity(patient)));
                    Assert.That(state.Parts, Is.Not.Empty);
                });
            });

            await pair.RunTicksSync(15);

            await client.WaitAssertion(() =>
            {
                var entities = client.EntMan;
                var clientSurgeon = entities.GetEntity(surgeonNet);
                Assert.That(entities.TryGetComponent<UserInterfaceComponent>(clientSurgeon, out var ui), Is.True);
                Assert.That(ui!.ClientOpenInterfaces.TryGetValue(CMUSurgeryUIKey.Key, out var openBui), Is.True);
                Assert.That(openBui, Is.TypeOf<CMUSurgeryBui>());

                var stateProperty = typeof(BoundUserInterface).GetProperty(
                    "State",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                var clientState = stateProperty.GetValue(openBui) as CMUSurgeryBuiState;
                var windowField = typeof(CMUSurgeryBui).GetField(
                    "_window",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                var window = windowField.GetValue(openBui) as CMUSurgeryWindow;

                Assert.Multiple(() =>
                {
                    Assert.That(clientState, Is.Not.Null);
                    Assert.That(clientState!.PatientName, Is.Not.Null.And.Not.Empty);
                    Assert.That(clientState.Parts, Is.Not.Null.And.Not.Empty);
                    Assert.That(clientState.Parts, Has.All.Property(nameof(CMUSurgeryPartEntry.EligibleSurgeries)).Not.Null);
                    Assert.That(window, Is.Not.Null);
                    Assert.That(window!.PatientLabel.TextMemory.Length, Is.GreaterThan(0));
                    Assert.That(window.PartListContainer.ChildCount, Is.GreaterThan(0));
                    Assert.That(window.ProcedureListContainer.ChildCount, Is.GreaterThan(0));
                });
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task DirectSurgeryToolStartsAndKeepsRunning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid scalpel = default;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            surgeon = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            scalpel = entities.SpawnEntity("CMScalpel", map.GridCoords);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            PreparePatient(entities, surgeon, patient);
            entities.System<ServerBodyZoneTargetingSystem>().SelectZone(surgeon, TargetBodyZone.Chest);
            Assert.That(entities.System<SharedHandsSystem>().TryPickup(surgeon, scalpel), Is.True);

            var dispatched = entities.System<CMUSurgeryDispatchSystem>()
                .TryDispatchUiLess(surgeon, patient, scalpel);

            Assert.Multiple(() =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(entities.System<CMUSurgerySessionSystem>().IsPerforming(patient), Is.True);
                Assert.That(entities.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.True);
            });
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<CMUSurgerySessionSystem>().IsPerforming(patient), Is.True));

        await pair.CleanReturnAsync();
    }

    private static void PreparePatient(IEntityManager entities, EntityUid surgeon, EntityUid patient)
    {
        entities.System<SkillsSystem>().SetSkill(surgeon, "RMCSkillSurgery", 3);
        entities.System<StandingStateSystem>().Down(
            patient,
            playSound: false,
            dropHeldItems: false,
            force: true);
    }
}
