using Content.Client.CMU14.Medical.Treatment.Surgery;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

/// <summary>Round-trips the missing-site wire projection and the real continuation button message.</summary>
[TestFixture]
public sealed class SurgeryMissingSiteUiTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    [TestCase(BodyPartType.Hand, BodyPartType.Arm)]
    [TestCase(BodyPartType.Foot, BodyPartType.Leg)]
    public async Task CommittedSocketOpeningProjectsAndContinuesTheMissingSite(
        BodyPartType type, BodyPartType anchorType)
    {
        EntityUid patient = default;
        EntityUid anchor = default;
        CMUSurgeryBuiState state = default!;
        CMUSurgerySelectionMessageProbeComponent probe = default!;
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<CMUSurgerySelectionMessageProbeSystem>();
            patient = SEntMan.SpawnEntity("CMMobHuman", MapData.GridCoords);
            SEntMan.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false, force: true);
            SEntMan.EnsureComponent<BypassSkillChecksComponent>(SPlayer);
            probe = SEntMan.AddComponent<CMUSurgerySelectionMessageProbeComponent>(SPlayer);
            var index = SEntMan.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetBodyPart(patient, new(anchorType, BodyPartSymmetry.Right), out anchor), Is.True);
            Assert.That(anchor, Is.Not.EqualTo(patient));

            // Both missing rows use the patient sentinel. The right slot is
            // the selected operation; the left slot must never win by order.
            foreach (var side in new[] { BodyPartSymmetry.Left, BodyPartSymmetry.Right })
            {
                Assert.That(index.TryGetBodyPart(patient, new(type, side), out var part), Is.True);
                Assert.That(SEntMan.System<DetachableOrganSystem>().Detach(part), Is.Not.Null);
            }

            var scalpel = SEntMan.SpawnEntity("CMScalpel", MapData.GridCoords);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(SPlayer, scalpel), Is.True);
            var flow = SEntMan.System<CMUSurgeryFlowSystem>();
            var armed = flow.TryArmStep(SPlayer, patient, anchor, "CMUSurgeryReattachLimb", 0,
                type, BodyPartSymmetry.Right);
            Assert.That(armed, Is.Not.Null);
            Assert.That(armed!.RequiredToolCategory, Is.EqualTo("scalpel"));
            Assert.That(flow.TryHandleArmedToolUse(patient, armed, SPlayer, scalpel, patient,
                out var handled, out var started), Is.True);
            Assert.That(handled && started, Is.True);
        });
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMIncisionOpenComponent>(anchor), Is.True,
                "The real tool DoAfter must commit on the parent socket.");
            Assert.That(SEntMan.GetComponent<CMUSurgeryInProgressComponent>(patient).Part, Is.EqualTo(anchor));
            Assert.That(SEntMan.GetComponent<CMUSurgeryArmedStepComponent>(patient).RequiredToolCategory,
                Is.EqualTo("hemostat"));
            // Releasing the next action preserves the committed opening and
            // models another medic resuming it through the operation window.
            SEntMan.System<CMUSurgeryFlowSystem>().ClearArmed(patient, popup: false);
            Assert.That(SEntMan.System<CMUSurgeryDispatchSystem>().TryDispatch(SPlayer, patient), Is.True);
            Assert.That(SUiSys.TryGetUiState<CMUSurgeryBuiState>(SPlayer, CMUSurgeryUIKey.Key, out var published), Is.True);
            state = published!;
            Assert.That(state.CurrentArmedStep, Is.Null);
            Assert.That(state.InFlight, Is.Not.Null);
            Assert.That(state.InFlight!.Part, Is.EqualTo(SEntMan.GetNetEntity(anchor)));
            Assert.That(state.InFlight.Part, Is.Not.EqualTo(state.Patient));
            Assert.That(state.SessionPartType, Is.EqualTo(type));
            Assert.That(state.SessionPartSymmetry, Is.EqualTo(BodyPartSymmetry.Right));
            var marked = state.Parts.Single(part => part.IsInFlightHere);
            Assert.That(marked.Part, Is.EqualTo(state.Patient));
            Assert.That(marked.Type, Is.EqualTo(type));
            Assert.That(marked.Symmetry, Is.EqualTo(BodyPartSymmetry.Right));
            Assert.That(marked.EligibleSurgeries.Single().NextStepToolCategory, Is.EqualTo("hemostat"));
            Assert.That(state.Parts.Single(part => part.Type == type && part.Symmetry == BodyPartSymmetry.Left).LockedByOtherPart,
                Is.True);
        });
        await RunUntilSynced();

        Button continuation = default!;
        await Client.WaitAssertion(() =>
        {
            var window = Descendants<CMUSurgeryWindow>(UiMan.WindowRoot).Single();
            Assert.That(window.SelectedPartLabel.Text, Is.EqualTo($"Right {type}"));
            // CurrentArmedStep is absent, so this label must resolve the actual
            // marked Rulebook row via Flow's distinct socket identity.
            Assert.That(window.InProgressStepLabel.Visible, Is.True);
            Assert.That(window.InProgressActionLabel.Visible, Is.True);
            var localization = Client.ResolveDependency<ILocalizationManager>();
            Assert.That(window.InProgressActionLabel.Text,
                Does.Contain(localization.GetString("cmu-medical-surgery-tool-category-hemostat")));
            continuation = Descendants<Button>(window.ProcedureListContainer).Single();
            Assert.That(continuation.Text, Is.EqualTo(localization.GetString("cmu-medical-surgery-continue-button")));
        });
        await ClickControl(continuation);
        await RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            Assert.That(probe.Last, Is.Not.Null);
            Assert.That(probe.Last!.Actor, Is.EqualTo(SPlayer));
            Assert.That(probe.Last.Patient, Is.EqualTo(state.Patient));
            Assert.That(probe.Last.Part, Is.EqualTo(state.Patient));
            Assert.That(probe.Last.TargetPartType, Is.EqualTo(type));
            Assert.That(probe.Last.TargetSymmetry, Is.EqualTo(BodyPartSymmetry.Right));
            Assert.That(probe.Last.ExpectedViewRevision, Is.EqualTo(state.ViewRevision));
            var armed = SEntMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
            Assert.That(armed.LeafSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
            Assert.That(armed.RequiredToolCategory, Is.EqualTo("hemostat"));
            Assert.That(armed.TargetPartType, Is.EqualTo(type));
            Assert.That(armed.TargetSymmetry, Is.EqualTo(BodyPartSymmetry.Right));
            Assert.That(SEntMan.GetComponent<CMUSurgeryInProgressComponent>(patient).Part, Is.EqualTo(anchor));
            SUiSys.CloseUi(SPlayer, CMUSurgeryUIKey.Key, SPlayer);
        });
        await RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(Descendants<CMUSurgeryWindow>(UiMan.WindowRoot), Is.Empty));
    }

    [TestCase(BodyPartType.Arm)]
    [TestCase(BodyPartType.Hand)]
    [TestCase(BodyPartType.Foot)]
    public async Task ContinuationUsesMarkedMissingSiteAcrossRefreshAndRowReordering(BodyPartType type)
    {
        EntityUid patient = default;
        NetEntity netPatient = default;
        CMUSurgerySelectionMessageProbeComponent probe = default!;
        CMUSurgeryWindow window = default!;
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<CMUSurgerySelectionMessageProbeSystem>();
            patient = SEntMan.SpawnEntity("CMMobHuman", MapData.GridCoords);
            netPatient = SEntMan.GetNetEntity(patient);
            probe = SEntMan.AddComponent<CMUSurgerySelectionMessageProbeComponent>(SPlayer);
            // Own this presentation input stream through the production BUI wire.
            // Server operation eligibility is covered by the surgery transaction suite.
            SUiSys.SetUiState(SPlayer, CMUSurgeryUIKey.Key, Projection(netPatient, type, BodyPartSymmetry.Right, false));
            SUiSys.OpenUi(SPlayer, CMUSurgeryUIKey.Key, SPlayer);
        });
        await RunTicks(15);
        await Client.WaitAssertion(() => window = Descendants<CMUSurgeryWindow>(UiMan.WindowRoot).Single());

        foreach (var side in new[] { BodyPartSymmetry.Right, BodyPartSymmetry.Left })
        {
            if (side == BodyPartSymmetry.Left)
            {
                await Server.WaitPost(() => SUiSys.SetUiState(SPlayer, CMUSurgeryUIKey.Key,
                    Projection(netPatient, type, side, true)));
                await RunTicks(10);
            }
            Button continuation = default!;
            await Client.WaitAssertion(() =>
            {
                Assert.That(window.SelectedPartLabel.Text, Does.Contain(type.ToString()));
                var choices = Descendants<Button>(window.InProgressChoiceContainer).ToArray();
                Assert.That(choices, Has.Length.EqualTo(1));
                continuation = choices[0];
                Assert.That(continuation.Text, Does.Contain($"Continue {side}"));
                Assert.That(continuation.Text, Does.Not.Contain($"Continue {(side == BodyPartSymmetry.Right ? BodyPartSymmetry.Left : BodyPartSymmetry.Right)}"));
            });
            await ClickControl(continuation);
            await RunTicks(10);
            await Server.WaitAssertion(() =>
            {
                Assert.That(probe.Last, Is.Not.Null);
                Assert.That(probe.Last!.Actor, Is.EqualTo(SPlayer));
                Assert.That(probe.Last.Patient, Is.EqualTo(netPatient));
                Assert.That(probe.Last.Part, Is.EqualTo(netPatient), "Missing slots deliberately share their patient's entity.");
                Assert.That(probe.Last.TargetPartType, Is.EqualTo(type));
                Assert.That(probe.Last.TargetSymmetry, Is.EqualTo(side));
                Assert.That(probe.Last.SurgeryId, Is.EqualTo($"CMUTestContinue{side}"));
                probe.Last = null;
            });
        }
        await Server.WaitPost(() => SUiSys.CloseUi(SPlayer, CMUSurgeryUIKey.Key, SPlayer));
        await RunTicks(5);
        await Client.WaitAssertion(() => Assert.That(Descendants<CMUSurgeryWindow>(UiMan.WindowRoot), Is.Empty));
    }

    private static CMUSurgeryBuiState Projection(NetEntity patient, BodyPartType type, BodyPartSymmetry active, bool reverse)
    {
        var parts = new List<CMUSurgeryPartEntry>();
        foreach (var side in reverse
                     ? new[] { BodyPartSymmetry.Right, BodyPartSymmetry.Left }
                     : new[] { BodyPartSymmetry.Left, BodyPartSymmetry.Right })
        {
            parts.Add(new CMUSurgeryPartEntry(patient, type, side, $"{side} {type}", "Missing",
                side == active, side != active,
                [new CMUSurgeryEntry("CMUTestLeaf", "Current operation", $"Current step {side}", null, 0, 2, null, "transplant"),
                 new CMUSurgeryEntry($"CMUTestContinue{side}", $"Continue {side}", "Next operation", null, 0, 2, null, "transplant")]));
        }
        return new CMUSurgeryBuiState(patient, "Missing-site patient", parts, null,
            new CMUSurgeryInFlightInfo(patient, $"{active} {type}", "CMUTestLeaf", "Current operation", "Medic", TimeSpan.Zero),
            null, null, null, null, type, active);
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (var child in root.Children)
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}

[RegisterComponent]
public sealed partial class CMUSurgerySelectionMessageProbeComponent : Component
{
    public CMUSurgeryArmStepMessage? Last;
}

public sealed partial class CMUSurgerySelectionMessageProbeSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<CMUSurgerySelectionMessageProbeComponent, CMUSurgeryArmStepMessage>(OnSelection);

    private void OnSelection(Entity<CMUSurgerySelectionMessageProbeComponent> ent, ref CMUSurgeryArmStepMessage args)
        => ent.Comp.Last = args;
}
