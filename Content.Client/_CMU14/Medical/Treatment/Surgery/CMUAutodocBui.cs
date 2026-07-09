using System;
using System.Numerics;
using Content.Client._CMU14.Medical.Presentation.Windows;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Medical.Treatment.Surgery;

[UsedImplicitly]
public sealed partial class CMUAutodocBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private ILocalizationManager _localization = default!;
    [Dependency] private IPlayerManager _players = default!;

    private CMUAutodocWindow? _window;
    private CMUAutodocBuiState? _latestState;
    private CMUSurgeryPartKey? _selectedPart;

    public CMUAutodocBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUAutodocWindow>();
        _window.Title = Loc.GetString("cmu-autodoc-window-title");
        _window.StartButton.OnPressed += StartPressed;
        _window.StopButton.OnPressed += StopPressed;
        _window.ClearButton.OnPressed += ClearPressed;
        _window.EjectButton.OnPressed += EjectPressed;

        if (_latestState is { } state)
            Refresh(state);
        else if (State is CMUAutodocBuiState legacyState)
            Refresh(legacyState);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not CMUAutodocStateMessage update)
            return;

        _latestState = update.State;
        Refresh(update.State);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CMUAutodocBuiState autodoc)
        {
            _latestState = autodoc;
            Refresh(autodoc);
        }
    }

    private void Refresh(CMUAutodocBuiState state)
    {
        if (_window is null)
            return;

        var currentStep = state.CurrentStep is null
            ? Loc.GetString("cmu-autodoc-current-idle")
            : Loc.GetString("cmu-autodoc-current-step", ("step", ResolveLabel(state.CurrentStep)));
        if (state.Running && state.NextStepAt is { } nextStepAt && state.CurrentStep is not null)
        {
            currentStep = Loc.GetString(
                "cmu-autodoc-current-step-timed",
                ("step", ResolveLabel(state.CurrentStep)),
                ("time", FormatRemaining(nextStepAt)));
        }

        _window.SetPatient(ResolvePatient(state.Patient), state.PatientName, state.Status, currentStep, _entities, _players);
        _window.StatusLabel.Text = state.Status;
        _window.CurrentStepLabel.Text = currentStep;
        _window.QueueSummaryLabel.Text = state.Queue.Count == 0
            ? Loc.GetString("cmu-autodoc-queue-empty")
            : Loc.GetString("cmu-autodoc-queue-summary", ("count", state.Queue.Count));

        _window.StartButton.Disabled = !state.CanQueue || state.Running || state.Queue.Count == 0;
        _window.StopButton.Disabled = !state.CanQueue || !state.Running;
        _window.ClearButton.Disabled = !state.CanQueue || state.Queue.Count == 0;
        _window.EjectButton.Disabled = !state.CanQueue || state.Patient is null;

        RefreshQueue(state);
        RefreshSurgeryList(state);
    }

    private EntityUid? ResolvePatient(NetEntity? patient)
    {
        if (patient is not { } netPatient)
            return null;

        var uid = _entities.GetEntity(netPatient);
        return uid.Valid ? uid : null;
    }

    private void StartPressed(BaseButton.ButtonEventArgs args) => SendMessage(new CMUAutodocStartMessage());

    private void StopPressed(BaseButton.ButtonEventArgs args) => SendMessage(new CMUAutodocStopMessage());

    private void ClearPressed(BaseButton.ButtonEventArgs args) => SendMessage(new CMUAutodocClearQueueMessage());

    private void EjectPressed(BaseButton.ButtonEventArgs args) => SendMessage(new CMUAutodocEjectPatientMessage());

    private void RefreshQueue(CMUAutodocBuiState state)
    {
        if (_window is null)
            return;

        _window.QueueList.DisposeAllChildren();
        if (state.Queue.Count == 0)
        {
            _window.QueueList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-autodoc-queue-empty")));
            return;
        }

        foreach (var entry in state.Queue)
            _window.QueueList.AddChild(BuildQueueRow(state, entry));
    }

    private Control BuildQueueRow(CMUAutodocBuiState state, CMUAutodocQueueEntry entry)
    {
        var active = entry.Index == 0 && state.Running;
        var accent = active ? CMUMedicalMachineStyle.Warning : CMUMedicalMachineStyle.Blue;
        var panel = CMUMedicalMachineStyle.Panel(
            active ? Color.FromHex("#272117") : CMUMedicalMachineStyle.DeepCardBg,
            accent,
            active ? new Thickness(2) : new Thickness(1));

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(7, 5),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        row.AddChild(new Label
        {
            Text = (entry.Index + 1).ToString(),
            MinWidth = 26,
            Align = Label.AlignMode.Center,
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = accent,
            StyleClasses = { "LabelKeyText" },
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = $"{entry.SurgeryDisplayName} - {entry.PartDisplayName}",
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });
        text.AddChild(new Label
        {
            Text = Loc.GetString(
                "cmu-autodoc-procedure-time-note",
                ("time", FormatDuration(entry.DurationSeconds))),
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = active ? CMUMedicalMachineStyle.Warning : CMUMedicalMachineStyle.Muted,
        });

        var remove = new Button
        {
            Text = Loc.GetString("cmu-autodoc-remove-button"),
            Disabled = !state.CanQueue,
            MinWidth = 72,
            VerticalAlignment = Control.VAlignment.Center,
        };
        var index = entry.Index;
        remove.OnPressed += _ => SendMessage(new CMUAutodocRemoveQueueStepMessage(index));
        row.AddChild(remove);

        return panel;
    }

    private void RefreshSurgeryList(CMUAutodocBuiState state)
    {
        if (_window is null)
            return;

        _window.PartList.DisposeAllChildren();
        _window.SurgeryList.DisposeAllChildren();

        if (!state.CanQueue)
        {
            _window.SelectedPartLabel.Text = Loc.GetString("cmu-autodoc-parts-heading");
            _window.SelectedPartStatusLabel.Text = Loc.GetString("cmu-autodoc-surgery2-required");
            _window.PartList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-autodoc-surgery2-required")));
            _window.SurgeryList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-autodoc-surgery2-required")));
            return;
        }

        EnsureSelectedPart(state);
        foreach (var part in state.Parts)
        {
            var selected = _selectedPart is { } key && key.Matches(part);
            var button = BuildPartButton(part, selected);
            var captured = part;
            button.OnPressed += _ =>
            {
                _selectedPart = new CMUSurgeryPartKey(captured);
                RefreshSurgeryList(state);
            };
            _window.PartList.AddChild(button);
        }

        if (!TryGetSelectedPart(state, out var selectedPart))
        {
            _window.SelectedPartLabel.Text = Loc.GetString("cmu-medical-surgery-no-part-selected");
            _window.SelectedPartStatusLabel.Text = Loc.GetString("cmu-autodoc-no-surgeries");
            _window.SurgeryList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-autodoc-no-surgeries")));
            return;
        }

        _window.SelectedPartLabel.Text = selectedPart.DisplayName;
        _window.SelectedPartStatusLabel.Text = selectedPart.EligibleSurgeries.Count == 0
            ? Loc.GetString("cmu-autodoc-no-surgeries")
            : Loc.GetString("cmu-autodoc-available-procedures", ("count", selectedPart.EligibleSurgeries.Count));

        if (selectedPart.EligibleSurgeries.Count == 0)
        {
            _window.SurgeryList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-autodoc-no-surgeries")));
            return;
        }

        foreach (var surgery in selectedPart.EligibleSurgeries)
            _window.SurgeryList.AddChild(BuildSurgeryRow(selectedPart, surgery));
    }

    private Button BuildPartButton(CMUSurgeryPartEntry part, bool selected)
    {
        var accent = selected
            ? CMUMedicalMachineStyle.Warning
            : part.EligibleSurgeries.Count > 0 ? CMUMedicalMachineStyle.Cyan : CMUMedicalMachineStyle.Dim;

        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 52,
            ModulateSelfOverride = selected ? Color.White : Color.FromHex("#CDD6DE"),
        };

        var panel = CMUMedicalMachineStyle.Panel(
            selected ? Color.FromHex("#211F2A") : CMUMedicalMachineStyle.DeepCardBg,
            accent,
            selected ? new Thickness(2) : new Thickness(1));

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            Margin = new Thickness(7, 5),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 34),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = part.DisplayName,
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });
        text.AddChild(new Label
        {
            Text = part.EligibleSurgeries.Count == 0
                ? Loc.GetString("cmu-medical-surgery-part-condition-no-eligible")
                : Loc.GetString("cmu-autodoc-part-procedures", ("count", part.EligibleSurgeries.Count)),
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
        });

        button.AddChild(panel);
        return button;
    }

    private Control BuildSurgeryRow(CMUSurgeryPartEntry part, CMUSurgeryEntry surgery)
    {
        var panel = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.DeepCardBg, CMUMedicalMachineStyle.Purple);
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(7, 5),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 42),
            PanelOverride = CMUMedicalMachineStyle.Flat(CMUMedicalMachineStyle.Purple, CMUMedicalMachineStyle.Purple),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = surgery.DisplayName,
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });
        text.AddChild(new Label
        {
            Text = Loc.GetString(
                "cmu-autodoc-procedure-time-note",
                ("time", FormatDuration(GetAutodocDurationSeconds(surgery)))),
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
        });

        var button = new Button
        {
            Text = Loc.GetString("cmu-autodoc-queue-button"),
            MinWidth = 86,
            VerticalAlignment = Control.VAlignment.Center,
        };
        button.OnPressed += _ => SendMessage(new CMUAutodocQueueStepMessage(
            part.Part,
            part.Type,
            part.Symmetry,
            surgery.SurgeryId,
            surgery.NextStepIndex));
        row.AddChild(button);

        return panel;
    }

    private void EnsureSelectedPart(CMUAutodocBuiState state)
    {
        if (state.Parts.Count == 0)
        {
            _selectedPart = null;
            return;
        }

        if (_selectedPart is { } selected && CMUSurgeryPartKey.Contains(state.Parts, selected))
            return;

        foreach (var part in state.Parts)
        {
            if (part.EligibleSurgeries.Count == 0)
                continue;

            _selectedPart = new CMUSurgeryPartKey(part);
            return;
        }

        _selectedPart = new CMUSurgeryPartKey(state.Parts[0]);
    }

    private bool TryGetSelectedPart(CMUAutodocBuiState state, out CMUSurgeryPartEntry part)
    {
        if (_selectedPart is { } selected && CMUSurgeryPartKey.TryFind(state.Parts, selected, out part))
            return true;

        part = default!;
        return false;
    }

    private static float GetAutodocDurationSeconds(CMUSurgeryEntry surgery)
    {
        if (surgery.SurgeryId == "CMUAutodocRepairWounds")
            return 120f;

        if (surgery.SurgeryId.Contains("Shattered", StringComparison.OrdinalIgnoreCase))
            return 240f;

        if (surgery.SurgeryId.Contains("Compound", StringComparison.OrdinalIgnoreCase))
            return 180f;

        if (surgery.SurgeryId.Contains("Simple", StringComparison.OrdinalIgnoreCase))
            return 120f;

        return surgery.Category switch
        {
            "fracture" => 180f,
            "bleed" => 180f,
            "suture" => 240f,
            "head_organ" => 240f,
            _ => 180f,
        };
    }

    private static string FormatDuration(float seconds)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(seconds / 60f));
        return Loc.GetString("cmu-autodoc-minutes", ("minutes", minutes));
    }

    private static string FormatRemaining(TimeSpan expiresAt)
    {
        var timing = IoCManager.Resolve<IGameTiming>();
        var remaining = expiresAt - timing.CurTime;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        return $"{Math.Ceiling(remaining.TotalSeconds)}s";
    }

    private string ResolveLabel(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "-";

        return _localization.TryGetString(text, out var localized) ? localized : text;
    }
}

public sealed partial class CMUAutodocWindow : FancyWindow
{
    private const string RememberedSizeKey = "cmu-autodoc";
    private static readonly Vector2 PreferredWindowSize = new(1080f, 690f);
    private static readonly Vector2 MinimumWindowSize = new(700f, 460f);

    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly CMUMedicalUniformScaler _uniformScaler = new();
    private readonly PanelContainer _scaleRoot;
    private readonly SpriteView _patientPreview;
    private readonly Label _previewFallbackLabel;

    public readonly Label PatientLabel;
    public readonly Label StatusLabel;
    public readonly Label CurrentStepLabel;
    public readonly Label QueueSummaryLabel;
    public readonly Label SelectedPartLabel;
    public readonly Label SelectedPartStatusLabel;
    public readonly BoxContainer QueueList;
    public readonly BoxContainer PartList;
    public readonly BoxContainer SurgeryList;
    public readonly Button StartButton;
    public readonly Button StopButton;
    public readonly Button ClearButton;
    public readonly Button EjectButton;

    private float _layoutScale = 1f;

    public CMUAutodocWindow()
    {
        IoCManager.InjectDependencies(this);
        AllowDraggingOutsideParentBounds = true;

        SetSize = CMUMedicalWindowSizing.GetInitialSize(RememberedSizeKey, PreferredWindowSize);
        MinSize = MinimumWindowSize;
        SetCloseButtonAppearance(CMUMedicalMachineStyle.Text, new Vector2(18f, 18f));

        _scaleRoot = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = CMUMedicalMachineStyle.Flat(CMUMedicalMachineStyle.Surface, CMUMedicalMachineStyle.Border),
        };
        AddChild(_scaleRoot);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _scaleRoot.AddChild(root);

        var titleBar = CMUMedicalMachineStyle.WindowHeader(Loc.GetString("cmu-autodoc-window-title"), out var closeButton);
        closeButton.OnPressed += _ => Close();
        root.AddChild(titleBar);

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            MinHeight = 150,
        };
        root.AddChild(header);

        var patientCard = CMUMedicalMachineStyle.Panel(Color.FromHex("#101922"), Color.FromHex("#345064"), new Thickness(2));
        patientCard.MinWidth = 420;
        patientCard.HorizontalExpand = true;
        header.AddChild(patientCard);

        var patientRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        patientCard.AddChild(patientRow);

        var previewFrame = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.WindowBg, CMUMedicalMachineStyle.Blue);
        previewFrame.MinSize = new Vector2(92, 92);
        previewFrame.HorizontalExpand = false;
        patientRow.AddChild(previewFrame);

        _patientPreview = new SpriteView
        {
            SetSize = new Vector2(88, 88),
            OverrideDirection = Direction.South,
            Stretch = SpriteView.StretchMode.Fit,
            Scale = new Vector2(1.55f, 1.55f),
        };
        previewFrame.AddChild(_patientPreview);

        _previewFallbackLabel = new Label
        {
            Text = Loc.GetString("cmu-autodoc-no-patient"),
            Align = Label.AlignMode.Center,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
            FontColorOverride = CMUMedicalMachineStyle.Dim,
            Visible = false,
        };
        previewFrame.AddChild(_previewFallbackLabel);

        var patientText = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        patientRow.AddChild(patientText);

        patientText.AddChild(new Label
        {
            Text = Loc.GetString("cmu-medical-surgery-section-patient"),
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
        });

        PatientLabel = new Label
        {
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            HorizontalExpand = true,
        };
        patientText.AddChild(PatientLabel);

        StatusLabel = new Label
        {
            FontColorOverride = CMUMedicalMachineStyle.Cyan,
            ClipText = true,
            HorizontalExpand = true,
        };
        patientText.AddChild(StatusLabel);

        CurrentStepLabel = new Label
        {
            FontColorOverride = CMUMedicalMachineStyle.Warning,
            ClipText = true,
            HorizontalExpand = true,
        };
        patientText.AddChild(CurrentStepLabel);

        var workflowCard = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.CardBg, CMUMedicalMachineStyle.Border);
        workflowCard.MinWidth = 320;
        header.AddChild(workflowCard);

        var workflow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        workflowCard.AddChild(workflow);

        workflow.AddChild(new Label
        {
            Text = Loc.GetString("cmu-medical-surgery-section-workflow"),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });

        QueueSummaryLabel = MakeMetricLabel(workflow, Loc.GetString("cmu-autodoc-queue-heading"), CMUMedicalMachineStyle.Blue);

        var controlsCard = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.CardBg, CMUMedicalMachineStyle.Border);
        controlsCard.MinWidth = 220;
        header.AddChild(controlsCard);

        var controls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        controlsCard.AddChild(controls);

        controls.AddChild(new Label
        {
            Text = Loc.GetString("cmu-medical-surgery-actions-heading"),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });

        StartButton = CMUMedicalMachineStyle.ActionButton(Loc.GetString("cmu-autodoc-start-button"), CMUMedicalMachineStyle.Cyan);
        StopButton = CMUMedicalMachineStyle.ActionButton(Loc.GetString("cmu-autodoc-stop-button"), CMUMedicalMachineStyle.Warning);
        ClearButton = CMUMedicalMachineStyle.ActionButton(Loc.GetString("cmu-autodoc-clear-button"), CMUMedicalMachineStyle.Blue);
        EjectButton = CMUMedicalMachineStyle.ActionButton(Loc.GetString("cmu-autodoc-eject-button"), CMUMedicalMachineStyle.Red);
        controls.AddChild(StartButton);
        controls.AddChild(StopButton);
        controls.AddChild(ClearButton);
        controls.AddChild(EjectButton);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(body);

        QueueList = CMUMedicalMachineStyle.MakeTitledList(body, Loc.GetString("cmu-autodoc-queue-heading"), 320);
        PartList = CMUMedicalMachineStyle.MakeTitledList(body, Loc.GetString("cmu-autodoc-parts-heading"), 270);

        var procedurePanel = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.CardBg, CMUMedicalMachineStyle.Border);
        procedurePanel.HorizontalExpand = true;
        procedurePanel.VerticalExpand = true;
        body.AddChild(procedurePanel);

        var procedureRoot = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        procedurePanel.AddChild(procedureRoot);

        var selectedHeader = CMUMedicalMachineStyle.Panel(Color.FromHex("#211F2A"), CMUMedicalMachineStyle.Purple);
        procedureRoot.AddChild(selectedHeader);

        var selectedHeaderRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 6),
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        selectedHeader.AddChild(selectedHeaderRow);

        var selectedText = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        selectedHeaderRow.AddChild(selectedText);

        SelectedPartLabel = new Label
        {
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            HorizontalExpand = true,
        };
        selectedText.AddChild(SelectedPartLabel);

        SelectedPartStatusLabel = new Label
        {
            FontColorOverride = CMUMedicalMachineStyle.Muted,
            ClipText = true,
            HorizontalExpand = true,
        };
        selectedText.AddChild(SelectedPartStatusLabel);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        procedureRoot.AddChild(scroll);

        SurgeryList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(SurgeryList);

        CMUMedicalWindowSizing.FitToScreen(this, PreferredWindowSize, MinimumWindowSize, clampPosition: false);
        ApplyUniformScale(true);
    }

    public void SetPatient(
        EntityUid? patient,
        string patientName,
        string status,
        string currentStep,
        IEntityManager entities,
        IPlayerManager players)
    {
        PatientLabel.Text = patientName;
        StatusLabel.Text = status;
        CurrentStepLabel.Text = currentStep;

        var showPreview = patient is { } uid &&
                          uid.Valid &&
                          entities.HasComponent<SpriteComponent>(uid);

        _patientPreview.Visible = showPreview;
        _previewFallbackLabel.Visible = !showPreview;

        if (!showPreview || patient is not { } preview)
            return;

        _patientPreview.SetEntity(preview);
        _patientPreview.ModulateSelfOverride = GhostPreviewHelper.CanUseLiveSprite(entities, players, preview)
            ? Color.White
            : Color.FromHex("#9AA3AD");
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        CMUMedicalWindowSizing.FitToScreen(this, PreferredWindowSize, MinimumWindowSize, clampPosition: false);
        ApplyUniformScale();
        CMUMedicalWindowSizing.RememberSize(RememberedSizeKey, this);
    }

    private void ApplyUniformScale(bool force = false)
    {
        var size = Size.X > 0f && Size.Y > 0f ? Size : SetSize;
        var scale = Math.Clamp(
            Math.Min(size.X / PreferredWindowSize.X, size.Y / PreferredWindowSize.Y),
            CMUMedicalUniformScaler.MinimumScale,
            1f);

        if (!force && Math.Abs(_layoutScale - scale) < 0.001f)
            return;

        _layoutScale = scale;
        _uniformScaler.Apply(_scaleRoot, _layoutScale, _resourceCache);
    }

    private static Label MakeMetricLabel(BoxContainer parent, string title, Color accent)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 34),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = title,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
            ClipText = true,
        });

        var value = new Label
        {
            FontColorOverride = accent,
            ClipText = true,
            HorizontalExpand = true,
        };
        text.AddChild(value);

        parent.AddChild(CMUMedicalMachineStyle.Wrap(row, CMUMedicalMachineStyle.DeepCardBg, CMUMedicalMachineStyle.MutedBorder));
        return value;
    }
}
