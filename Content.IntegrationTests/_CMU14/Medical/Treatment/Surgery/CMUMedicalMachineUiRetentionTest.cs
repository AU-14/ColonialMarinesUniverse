using System.Numerics;
using Content.Client.CMU14.Medical.Treatment.Surgery;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUMedicalMachineUiRetentionTest : CMUMedicalMachineUiTestBase
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task FixedSizeStateRefreshKeepsControlsAndScalesNewRows(bool scanner)
    {
        var rig = await OpenMachine(scanner);
        Control retained = default!;
        Label oldLabel = default!;
        Label oldValue = default!;
        string oldValueText = string.Empty;
        float scaledMargin = 0;
        await Client.WaitAssertion(() =>
        {
            rig.Window.SetSize = scanner ? new Vector2(700, 460) : new Vector2(720, 480);
        });
        await RunTicks(3);
        await SendProjectedState(rig, 0, burst: false);
        await Client.WaitAssertion(() =>
        {
            retained = FindDataRow(rig, 0);
            oldLabel = Descendants<Label>(retained).First(label => label.TextMemory.Length > 0);
            oldValue = scanner
                ? Descendants<Label>(retained).Single(label => label.TextMemory.Span.SequenceEqual(
                    Loc.GetString("cmu-body-scanner-part-health", ("current", 80f), ("max", 100f)).AsSpan()))
                : Descendants<Label>(retained).Single(label => label.StyleClasses.Contains("LabelSubText"));
            oldValueText = oldValue.TextMemory.ToString();
            scaledMargin = Descendants<Control>(retained).Select(control => control.Margin.Left)
                .First(value => value > 0);
            Assert.That(scaledMargin, Is.LessThan(7), "the fixed window is below its preferred size");
        });
        await SendProjectedState(rig, 1, burst: true);
        await Client.WaitAssertion(() =>
        {
            Assert.That(FindDataRow(rig, 0), Is.SameAs(retained), "numeric state changes preserve the actual anatomy/queue row");
            Assert.That(oldLabel.Disposed, Is.False);
            Assert.DoesNotThrow(() => _ = oldLabel.Text, "scaling must preserve the public Label.Text contract");
            Assert.That(Descendants<Label>(retained), Does.Contain(oldValue));
            Assert.That(oldValue.TextMemory.ToString(), Is.Not.EqualTo(oldValueText), "retention must still update its current value");
            if (scanner)
            {
                Assert.That(oldValue.TextMemory.ToString(), Is.EqualTo(Loc.GetString(
                    "cmu-body-scanner-part-health", ("current", 79f), ("max", 100f))));
                Assert.That(Descendants<Label>(retained).Select(label => label.TextMemory.ToString()),
                    Is.SupersetOf(new[] { "Wound", "Eschar", "Fracture" }));
            }
            var newRow = FindDataRow(rig, 31);
            var newLabel = Descendants<Label>(newRow).First(label => label.TextMemory.Length > 0);
            Assert.That(newLabel.FontOverride, Is.Not.Null, "rows created after resize inherit its current font scale");
            var newMargin = Descendants<Control>(newRow).Select(control => control.Margin.Left).First(value => value > 0);
            Assert.That(newMargin, Is.EqualTo(scaledMargin).Within(0.01));
        });
    }

    [Test]
    public async Task RetainedQueueRemoveUsesStableIdAndLatestRevisionAfterEarlierRowRemoval()
    {
        var rig = await OpenMachine(false);
        ulong first = 0;
        ulong second = 0;
        await Server.WaitAssertion(() =>
        {
            foreach (var symmetry in new[] { BodyPartSymmetry.Left, BodyPartSymmetry.Right })
            {
                Assert.That(SEntMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(rig.Patient,
                    new(BodyPartType.Hand, symmetry), out var hand), Is.True);
                var health = SEntMan.GetComponent<BodyPartHealthComponent>(hand);
                SEntMan.System<SharedBodyPartHealthSystem>().SetCurrent((hand, health), health.Max - 10);
                var state = AutodocState(rig);
                var message = new CMUAutodocQueueStepMessage(SEntMan.GetNetEntity(hand), BodyPartType.Hand,
                    symmetry, "CMUAutodocRepairWounds", 0, state.CommandContext!.Value)
                { Actor = SPlayer, UiKey = CMUAutodocUIKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(rig.Console, message);
            }
            var queue = SEntMan.GetComponent<CMUAutodocPodComponent>(rig.Pod).Queue;
            Assert.That(queue, Has.Count.EqualTo(2));
            first = queue[0].Id;
            second = queue[1].Id;
        });
        await PushRealState(rig);
        Control secondRow = default!;
        Button remove = default!;
        await Client.WaitAssertion(() =>
        {
            var window = (CMUAutodocWindow) rig.Window;
            secondRow = window.QueueList.Children.ElementAt(1);
            remove = Descendants<Button>(secondRow).Single();
        });
        await Server.WaitAssertion(() =>
        {
            var message = new CMUAutodocRemoveQueueStepMessage(first, AutodocState(rig).CommandContext!.Value)
                { Actor = SPlayer, UiKey = CMUAutodocUIKey.Key };
            SEntMan.EventBus.RaiseLocalEvent(rig.Console, message);
        });
        await PushRealState(rig);
        await Client.WaitAssertion(() => Assert.That(((CMUAutodocWindow) rig.Window).QueueList.Children.Single(),
            Is.SameAs(secondRow)));
        await ClickControl(remove);
        await RunTicks(15);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUAutodocPodComponent>(rig.Pod).Queue,
            Is.Empty, $"the retained remove button must target entry {second}, using the new revision"));
    }

    [Test]
    public async Task RetainedScannerSignalUsesCurrentAssignmentCountAndRevision()
    {
        var rig = await OpenMachine(true);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(rig.Patient,
                new(BodyPartType.Hand, BodyPartSymmetry.Left), out var hand), Is.True);
            Assert.That(SEntMan.System<SharedBoneSystem>().SeedFracture(hand, FractureSeverity.Simple), Is.True);
            Assert.That(SEntMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(rig.Patient,
                new(BodyPartType.Hand, BodyPartSymmetry.Right), out hand), Is.True);
            Assert.That(SEntMan.System<SharedBoneSystem>().SeedFracture(hand, FractureSeverity.Simple), Is.True);
            var console = SEntMan.GetComponent<CMUBodyScannerConsoleComponent>(rig.Console);
            console.PulseWindowSize = console.MinPulseWindowSize = console.PulseGraceSize = 1;
        });
        await PushRealState(rig);
        await ClickControl(((CMUBodyScannerWindow) rig.Window).ResetButton);
        await RunTicks(15);
        CMUBodyScannerBuiState state = default!;
        await Server.WaitAssertion(() =>
        {
            state = ScannerState(rig);
            Assert.That(state.CalibrationStartedAt, Is.Not.Null);
        });
        var layer = state.Targets.First(target => !target.IsDecoy).LayerId;
        Button layerButton = default!;
        await Client.WaitAssertion(() => layerButton = FindButton(((CMUBodyScannerWindow) rig.Window).TermList,
            state.Terms.Single(term => term.Id == layer).Text));
        await ClickControl(layerButton);
        var signals = state.Targets.Where(target => !target.IsDecoy && target.LayerId == layer).Take(2).ToArray();
        Assert.That(signals, Has.Length.EqualTo(2), "two fractured hands supply two signals in the same layer");
        Button first = default!;
        Button second = default!;
        await Client.WaitAssertion(() =>
        {
            first = FindButton(((CMUBodyScannerWindow) rig.Window).TargetList, signals[0].Text);
            second = FindButton(((CMUBodyScannerWindow) rig.Window).TargetList, signals[1].Text);
        });
        await ClickControl(first);
        await RunTicks(15);
        await Server.WaitAssertion(() => Assert.That(ScannerState(rig).Assignments, Has.Count.EqualTo(1)));
        await Client.WaitAssertion(() => Assert.That(FindButton(((CMUBodyScannerWindow) rig.Window).TargetList,
            signals[1].Text), Is.SameAs(second)));
        await ClickControl(second);
        await RunTicks(15);
        await Server.WaitAssertion(() =>
        {
            var current = ScannerState(rig);
            Assert.That(current.Assignments.Count == 2 || current.BoostExpiresAt != null, Is.True,
                "the second retained signal must submit the current assignment count and operator revision");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PermissionRestorationDoesNotReactivateObsoleteActionCallbacks(bool scanner)
    {
        var rig = await OpenMachine(scanner);
        await SendProjectedState(rig, 0, burst: false);
        Control old = default!;
        Button obsolete = default!;
        var delivered = 0;
        await Client.WaitAssertion(() =>
        {
            old = ActionRows(rig).Children.ElementAt(1);
            obsolete = old as Button ?? Descendants<Button>(old).Single();
            Assert.That(obsolete.Disabled, Is.False);
            obsolete.OnPressed += _ => delivered++;
        });
        await SendProjectedState(rig, 1, burst: false, authorized: false);
        await Client.WaitAssertion(() => Assert.That(old.Parent, Is.Null));
        await SendProjectedState(rig, 2, burst: false);
        Button current = default!;
        Label selection = default!;
        string before = string.Empty;
        await Client.WaitAssertion(() =>
        {
            var fresh = ActionRows(rig).Children.ElementAt(1);
            Assert.That(fresh, Is.Not.SameAs(old));
            current = fresh as Button ?? Descendants<Button>(fresh).Single();
            selection = scanner ? ((CMUBodyScannerWindow) rig.Window).SweepDetailLabel
                : ((CMUAutodocWindow) rig.Window).SelectedPartLabel;
            before = selection.TextMemory.ToString();
        });
        await ClickControl(obsolete);
        await Client.WaitAssertion(() =>
        {
            Assert.That(delivered, Is.EqualTo(1), "the real GUI key boundary reached the obsolete button");
            Assert.That(selection.TextMemory.ToString(), Is.EqualTo(before), "its released callback cannot select the restored row");
        });
        await ClickControl(current);
        await Client.WaitAssertion(() => Assert.That(selection.TextMemory.ToString(), Is.Not.EqualTo(before),
            "the corresponding current control remains usable"));
    }

    [Test]
    public async Task ScannerSectionsRestoredAfterHiddenResizeUseCurrentScale()
    {
        var rig = await OpenMachine(true);
        await Client.WaitPost(() => rig.Window.SetSize = new Vector2(700, 460));
        await RunTicks(3);
        await SendProjectedState(rig, 0, burst: false);
        Control banner = default!;
        Label heading = default!;
        float oldMargin = 0;
        await Client.WaitAssertion(() =>
        {
            banner = DataRows(rig).Children.First();
            heading = DataRows(rig).Children.OfType<Label>().Single();
            oldMargin = Descendants<Control>(banner).Select(control => control.Margin.Left).First(value => value > 0);
        });
        await SendProjectedState(rig, 1, burst: false, authorized: false);
        await Client.WaitAssertion(() =>
        {
            Assert.That(banner.Parent, Is.Null);
            Assert.That(heading.Parent, Is.Null);
            rig.Window.SetSize = new Vector2(1000, 660);
        });
        await RunTicks(3);
        await SendProjectedState(rig, 2, burst: false);
        await Client.WaitAssertion(() =>
        {
            Assert.That(DataRows(rig).Children.First(), Is.SameAs(banner));
            Assert.That(DataRows(rig).Children.OfType<Label>().Single(), Is.SameAs(heading));
            var restoredMargin = Descendants<Control>(banner).Select(control => control.Margin.Left).First(value => value > 0);
            var newMargin = Descendants<Control>(FindDataRow(rig, 0)).Select(control => control.Margin.Left).First(value => value > 0);
            Assert.That(restoredMargin, Is.GreaterThan(oldMargin), "the section was outside the tree during the resize");
            Assert.That(restoredMargin, Is.EqualTo(newMargin).Within(0.01), "reattached banner and fresh anatomy cards share current scale");
            Assert.That(heading.Margin.Left / 2, Is.EqualTo(newMargin / 7).Within(0.01), "the retained section heading is scaled too");
        });
    }

    private static Control FindDataRow(Machine rig, int index)
    {
        var title = rig.Scanner ? "Part " + index : $"Procedure {index} - Part {index}";
        return DataRows(rig).Children.Single(row => Descendants<Label>(row)
            .Any(label => label.TextMemory.Span.SequenceEqual(title.AsSpan())));
    }
}

/// <summary>Uses existing public UI/state boundaries; shared by original/candidate measurement builds.</summary>
public abstract class CMUMedicalMachineUiTestBase : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    protected sealed record Machine(EntityUid Console, EntityUid Pod, EntityUid Patient,
        EntityUid ClientConsole, bool Scanner, Content.Client.UserInterface.Controls.FancyWindow Window);

    protected async Task<Machine> OpenMachine(bool scanner)
    {
        EntityUid console = default, pod = default, patient = default;
        NetEntity netConsole = default;
        var before = new HashSet<Control>();
        await Client.WaitPost(() => before.UnionWith(Descendants<Control>(UiMan.WindowRoot)));
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillSurgery", 2);
            var coordinates = MapData.GridCoords;
            MapSystem.SetTile(MapData.Grid.Owner, MapData.Grid.Comp, MapData.Tile.GridIndices + new Vector2i(1, 0), MapData.Tile.Tile);
            console = SEntMan.SpawnEntity(scanner ? "CMUBodyScannerConsole" : "CMUAutodocConsole", coordinates);
            netConsole = SEntMan.GetNetEntity(console);
            pod = SEntMan.SpawnEntity(scanner ? "CMUBodyScannerPod" : "CMUAutodocPod", coordinates.Offset(new Vector2(1, 0)));
            patient = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            var container = scanner ? SEntMan.GetComponent<CMUBodyScannerPodComponent>(pod).BodyContainer
                : SEntMan.GetComponent<CMUAutodocPodComponent>(pod).BodyContainer;
            Assert.That(SEntMan.System<CMUMedicalPatientBaySystem>().TryInsertPatient(pod, container, patient), Is.True);
            SUiSys.OpenUi(console, scanner ? CMUBodyScannerUIKey.Key : CMUAutodocUIKey.Key, SPlayer);
        });
        await RunTicks(15);
        Machine result = default!;
        await Client.WaitAssertion(() =>
        {
            var window = Descendants<Content.Client.UserInterface.Controls.FancyWindow>(UiMan.WindowRoot)
                .Single(window => !before.Contains(window) && (scanner ? window is CMUBodyScannerWindow : window is CMUAutodocWindow));
            result = new Machine(console, pod, patient, CEntMan.GetEntity(netConsole), scanner, window);
        });
        return result;
    }

    protected CMUAutodocBuiState AutodocState(Machine rig) => SEntMan.System<CMUAutodocSystem>()
        .BuildStateForViewer(rig.Console, SEntMan.GetComponent<CMUAutodocConsoleComponent>(rig.Console), SPlayer);

    protected CMUBodyScannerBuiState ScannerState(Machine rig) => SEntMan.System<CMUBodyScannerSystem>()
        .BuildStateForViewer(rig.Console, SEntMan.GetComponent<CMUBodyScannerConsoleComponent>(rig.Console), SPlayer);

    protected async Task PushRealState(Machine rig)
    {
        await Server.WaitPost(() =>
        {
            if (rig.Scanner) SUiSys.ServerSendUiMessage(rig.Console, CMUBodyScannerUIKey.Key, new CMUBodyScannerStateMessage(ScannerState(rig)), SPlayer);
            else SUiSys.ServerSendUiMessage(rig.Console, CMUAutodocUIKey.Key, new CMUAutodocStateMessage(AutodocState(rig)), SPlayer);
        });
        await RunTicks(5);
    }

    protected async Task SendProjectedState(Machine rig, int sequence, bool burst, bool authorized = true)
    {
        var expectedStatus = $"Projected state {sequence}: {authorized}";
        await Server.WaitPost(() =>
        {
            // Synthetic presentation cases own their input stream. Pause only the
            // console's normal producer so its periodic natural projection cannot
            // replace the supplied state between delivery and GUI assertions.
            SEntMan.System<MetaDataSystem>().SetEntityPaused(rig.Console, true);
            var state = Projection(rig, sequence, burst, authorized);
            if (state is CMUBodyScannerBuiState scanner)
            {
                scanner.Status = expectedStatus;
                SUiSys.ServerSendUiMessage(rig.Console, CMUBodyScannerUIKey.Key, new CMUBodyScannerStateMessage(scanner), SPlayer);
            }
            else
            {
                ((CMUAutodocBuiState) state).Status = expectedStatus;
                SUiSys.ServerSendUiMessage(rig.Console, CMUAutodocUIKey.Key, new CMUAutodocStateMessage((CMUAutodocBuiState) state), SPlayer);
            }
        });
        for (var tick = 0; tick < 30; tick++)
        {
            await RunTicks(1);
            var received = false;
            await Client.WaitPost(() =>
            {
                var label = rig.Scanner ? ((CMUBodyScannerWindow) rig.Window).StatusLabel
                    : ((CMUAutodocWindow) rig.Window).StatusLabel;
                received = label.TextMemory.Span.SequenceEqual(expectedStatus.AsSpan());
            });
            if (received)
                return;
        }
        Assert.Fail($"The connected client did not receive {expectedStatus}.");
    }

    protected BoundUserInterfaceState Projection(Machine rig, int sequence, bool burst, bool authorized = true)
    {
        var patient = SEntMan.GetNetEntity(rig.Patient);
        var pod = SEntMan.GetNetEntity(rig.Pod);
        var now = SGameTiming.CurTime;
        var count = burst ? 32 : 4;
        if (!rig.Scanner)
        {
            var parts = new List<CMUSurgeryPartEntry>();
            var queue = new List<CMUAutodocQueueEntry>();
            for (var i = 0; i < count; i++)
            {
                var id = "CMUAutodocRepairWounds" + i;
                var procedure = new CMUSurgeryEntry(id, "Procedure " + i, "Repair", null, 0, 1, null, "wound", 15 + i);
                if (i < 4 && authorized) parts.Add(new CMUSurgeryPartEntry(patient, (BodyPartType) i,
                    BodyPartSymmetry.None, "Part " + i, "Damaged", false, false, [procedure]));
                queue.Add(new CMUAutodocQueueEntry(i, patient, (BodyPartType) (i % 4), BodyPartSymmetry.None,
                    "Part " + i, id, "Procedure " + i, "wound", 0, "Repair", 15 + i + sequence % 2, (ulong) i + 1));
            }
            return new CMUAutodocBuiState(pod, patient, "Patient", true, authorized, true,
                "Running", "Repair", now + TimeSpan.FromSeconds(30 - sequence % 10), parts, queue)
            { CommandContext = new CMUAutodocCommandContext(pod, patient, 1, (ulong) sequence + 1) };
        }

        var lines = new List<CMUBodyScannerScanLine>();
        for (var i = 0; i < count; i++)
            lines.Add(new CMUBodyScannerScanLine(CMUBodyScannerScanCategory.Body, CMUBodyScannerScanKind.BodyPart,
                CMUBodyScannerScanSeverity.Warning, "Part " + i, "Injured", burst ? ["Wound", "Eschar", "Fracture"] : ["Wound"],
                true, 80 - sequence % 10, 100));
        return new CMUBodyScannerBuiState(pod, patient, "Patient", true, authorized, false, "Ready", null,
            null, now, now + TimeSpan.FromSeconds(120), now, 2.4f, 0.25f, 0.26f, 0.12f,
            null, 0, null, CMUBodyScannerFeedbackKind.None, authorized ? lines : [],
            authorized ? [new("bone", "Bone layer"), new("organ", "Organ layer")] : [],
            authorized ? [new("left", "bone", "Left hand", "Fracture"), new("right", "bone", "Right hand", "Fracture")] : [], [])
        { CommandContext = authorized ? new CMUBodyScannerCommandContext(SEntMan.GetNetEntity(rig.Console), pod, patient, 1,
            (ulong) sequence + 1, 1) : null, CalibrationAttempt = 1, CanStartCalibration = false };
    }

    protected static BoxContainer DataRows(Machine rig) => rig.Scanner
        ? ((CMUBodyScannerWindow) rig.Window).ScanList : ((CMUAutodocWindow) rig.Window).QueueList;

    protected static BoxContainer ActionRows(Machine rig) => rig.Scanner
        ? ((CMUBodyScannerWindow) rig.Window).TermList : ((CMUAutodocWindow) rig.Window).PartList;

    protected static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (var child in root.Children)
        {
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    protected static Button FindButton(Control root, string label) => Descendants<Button>(root)
        .First(button => Descendants<Label>(button).Any(text => text.TextMemory.Span.SequenceEqual(label.AsSpan())));
}
