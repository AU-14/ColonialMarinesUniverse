using System;
using System.Numerics;
using System.Collections.Generic;
using Content.Client.CMU14.Medical.Presentation.Windows;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
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

namespace Content.Client.CMU14.Medical.Treatment.Surgery;

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
        // Initial state and attachment can queue the same BUI more than once.
        if (_window != null)
            return;

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
        _latestState = state;
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
        SetText(_window.StatusLabel, state.Status);
        SetText(_window.CurrentStepLabel, currentStep);
        SetText(_window.QueueSummaryLabel, state.Queue.Count == 0
            ? Loc.GetString("cmu-autodoc-queue-empty")
            : Loc.GetString("cmu-autodoc-queue-summary", ("count", state.Queue.Count)));

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

    private void StartPressed(BaseButton.ButtonEventArgs args)
    {
        if (_latestState?.CommandContext is { } context)
            SendMessage(new CMUAutodocStartMessage(context));
    }

    private void StopPressed(BaseButton.ButtonEventArgs args)
    {
        if (_latestState?.CommandContext is { } context)
            SendMessage(new CMUAutodocStopMessage(context));
    }

    private void ClearPressed(BaseButton.ButtonEventArgs args)
    {
        if (_latestState?.CommandContext is { } context)
            SendMessage(new CMUAutodocClearQueueMessage(context));
    }

    private void EjectPressed(BaseButton.ButtonEventArgs args)
    {
        if (_latestState?.CommandContext is { } context)
            SendMessage(new CMUAutodocEjectPatientMessage(context));
    }

    private readonly Dictionary<ulong, QueueRow> _queueRows = new();
    private readonly Dictionary<CMUSurgeryPartKey, PartRow> _partRows = new();
    private readonly Dictionary<string, ProcedureRow> _procedureRows = new();
    private readonly HashSet<ulong> _seenQueue = new();
    private readonly HashSet<CMUSurgeryPartKey> _seenParts = new();
    private readonly HashSet<string> _seenProcedures = new();
    private readonly List<Control> _addedControls = new();
    private CMUSurgeryPartKey? _procedurePart;
    private (NetEntity? Pod, NetEntity? Patient, ulong Generation)? _rowContext;
    private Control? _queueEmpty;
    private Control? _partsEmpty;
    private Control? _proceduresEmpty;

    private void RefreshQueue(CMUAutodocBuiState state)
    {
        if (_window == null)
            return;

        var context = (state.Pod, state.Patient, state.CommandContext?.OccupantGeneration ?? 0);
        if (_rowContext != context)
        {
            ClearRows(_queueRows);
            ClearRows(_partRows);
            ClearRows(_procedureRows);
            _selectedPart = null;
            _procedurePart = null;
            _rowContext = context;
        }

        _seenQueue.Clear();
        _addedControls.Clear();
        SetEmpty(_window.QueueList, ref _queueEmpty, state.Queue.Count == 0,
            Loc.GetString("cmu-autodoc-queue-empty"));
        foreach (var entry in state.Queue)
        {
            _seenQueue.Add(entry.Id);
            if (!_queueRows.TryGetValue(entry.Id, out var row))
            {
                row = new QueueRow(entry.Id, QueueRemovePressed);
                _queueRows.Add(entry.Id, row);
                _window.QueueList.AddChild(row);
                _addedControls.Add(row);
            }
            row.Update(entry, state);
            if (row.GetPositionInParent() != entry.Index)
                row.SetPositionInParent(entry.Index);
        }
        RemoveAbsent(_queueRows, _seenQueue);
        _window.ScaleAddedControls(_addedControls);
    }

    private void QueueRemovePressed(ulong id)
    {
        if (_latestState is not { CanQueue: true, CommandContext: { } context } state ||
            !_queueRows.ContainsKey(id) || !state.Queue.Exists(entry => entry.Id == id))
            return;
        SendMessage(new CMUAutodocRemoveQueueStepMessage(id, context));
    }

    private void RefreshSurgeryList(CMUAutodocBuiState state)
    {
        if (_window == null)
            return;

        _addedControls.Clear();
        _seenParts.Clear();
        EnsureSelectedPart(state);
        var noParts = !state.CanQueue || state.Parts.Count == 0;
        SetEmpty(_window.PartList, ref _partsEmpty, noParts, Loc.GetString(
            state.CanQueue ? "cmu-autodoc-no-surgeries" : "cmu-autodoc-surgery2-required"));
        if (state.CanQueue)
        {
            var index = 0;
            foreach (var part in state.Parts)
            {
                var key = new CMUSurgeryPartKey(part);
                _seenParts.Add(key);
                if (!_partRows.TryGetValue(key, out var row))
                {
                    row = new PartRow(key, PartPressed);
                    _partRows.Add(key, row);
                    _window.PartList.AddChild(row);
                    _addedControls.Add(row);
                }
                row.Update(part, _selectedPart == key);
                if (row.GetPositionInParent() != index)
                    row.SetPositionInParent(index);
                index++;
            }
        }
        RemoveAbsent(_partRows, _seenParts);

        if (_procedurePart != _selectedPart || !state.CanQueue)
        {
            ClearRows(_procedureRows);
            _procedurePart = _selectedPart;
        }
        _seenProcedures.Clear();
        if (state.CanQueue && TryGetSelectedPart(state, out var selectedPart))
        {
            SetText(_window.SelectedPartLabel, selectedPart.DisplayName);
            SetText(_window.SelectedPartStatusLabel, selectedPart.EligibleSurgeries.Count == 0
                ? Loc.GetString("cmu-autodoc-no-surgeries")
                : Loc.GetString("cmu-autodoc-available-procedures", ("count", selectedPart.EligibleSurgeries.Count)));
            SetEmpty(_window.SurgeryList, ref _proceduresEmpty, selectedPart.EligibleSurgeries.Count == 0,
                Loc.GetString("cmu-autodoc-no-surgeries"));
            var index = 0;
            foreach (var surgery in selectedPart.EligibleSurgeries)
            {
                _seenProcedures.Add(surgery.SurgeryId);
                if (!_procedureRows.TryGetValue(surgery.SurgeryId, out var row))
                {
                    row = new ProcedureRow(new CMUSurgeryPartKey(selectedPart), surgery.SurgeryId, ProcedurePressed);
                    _procedureRows.Add(surgery.SurgeryId, row);
                    _window.SurgeryList.AddChild(row);
                    _addedControls.Add(row);
                }
                row.Update(surgery, selectedPart, state);
                if (row.GetPositionInParent() != index)
                    row.SetPositionInParent(index);
                index++;
            }
        }
        else
        {
            SetText(_window.SelectedPartLabel, Loc.GetString("cmu-medical-surgery-no-part-selected"));
            var empty = Loc.GetString(state.CanQueue ? "cmu-autodoc-no-surgeries" : "cmu-autodoc-surgery2-required");
            SetText(_window.SelectedPartStatusLabel, empty);
            SetEmpty(_window.SurgeryList, ref _proceduresEmpty, true, empty);
        }
        RemoveAbsent(_procedureRows, _seenProcedures);
        _window.ScaleAddedControls(_addedControls);
    }

    private void PartPressed(CMUSurgeryPartKey key)
    {
        if (_latestState is not { CanQueue: true } state || !CMUSurgeryPartKey.Contains(state.Parts, key))
            return;
        _selectedPart = key;
        RefreshSurgeryList(state);
    }

    private void ProcedurePressed(CMUSurgeryPartKey key, string id)
    {
        if (_latestState is not { CanQueue: true, CommandContext: { } context } state ||
            _selectedPart != key || !_procedureRows.ContainsKey(id) ||
            !CMUSurgeryPartKey.TryFind(state.Parts, key, out var part) ||
            state.Queue.Count >= CMUAutodocPodComponent.MaximumQueueEntries ||
            state.Queue.Exists(entry => entry.Type == key.Type && entry.Symmetry == key.Symmetry && entry.SurgeryId == id))
            return;

        var surgery = part.EligibleSurgeries.Find(entry => entry.SurgeryId == id);
        if (surgery != null)
            SendMessage(new CMUAutodocQueueStepMessage(key.Part, key.Type, key.Symmetry,
                id, surgery.NextStepIndex, context));
    }

    private void SetEmpty(BoxContainer parent, ref Control? empty, bool visible, string text)
    {
        if (!visible)
        {
            empty?.Orphan();
            empty = null;
            return;
        }
        if (empty != null)
        {
            foreach (var child in empty.Children)
                if (child is Label label) SetText(label, text);
            return;
        }
        empty = CMUMedicalMachineStyle.Empty(text);
        parent.AddChild(empty);
        _addedControls.Add(empty);
    }

    private static void SetText(Label label, string text)
    {
        if (!label.TextMemory.Span.SequenceEqual(text.AsSpan()))
            label.Text = text;
    }

    private static void ClearRows<TKey, TRow>(Dictionary<TKey, TRow> rows)
        where TKey : notnull where TRow : AutodocRow
    {
        foreach (var row in rows.Values)
            row.Release();
        rows.Clear();
    }

    private static void RemoveAbsent<TKey, TRow>(Dictionary<TKey, TRow> rows, HashSet<TKey> seen)
        where TKey : notnull where TRow : AutodocRow
    {
        // Dictionary removal does not invalidate its enumerator on the supported runtime.
        foreach (var (key, row) in rows)
        {
            if (seen.Contains(key))
                continue;
            row.Release();
            rows.Remove(key);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearRows(_queueRows);
            ClearRows(_partRows);
            ClearRows(_procedureRows);
            _latestState = null;
            _addedControls.Clear();
        }
        base.Dispose(disposing);
    }

    private abstract class AutodocRow : PanelContainer
    {
        protected readonly BoxContainer Row;
        protected readonly Label TitleLabel;
        protected readonly Label DetailLabel;
        protected readonly StyleBoxFlat Style;
        protected bool Released;

        protected AutodocRow(Color accent)
        {
            HorizontalExpand = true;
            Style = CMUMedicalMachineStyle.Flat(CMUMedicalMachineStyle.DeepCardBg, accent);
            PanelOverride = Style;
            Row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8, Margin = new Thickness(7, 5), HorizontalExpand = true };
            AddChild(Row);
            var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            Row.AddChild(text);
            TitleLabel = new Label { ClipText = true, HorizontalExpand = true,
                FontColorOverride = CMUMedicalMachineStyle.Text };
            DetailLabel = new Label { ClipText = true, HorizontalExpand = true,
                FontColorOverride = CMUMedicalMachineStyle.Muted, StyleClasses = { "LabelSubText" } };
            text.AddChild(TitleLabel);
            text.AddChild(DetailLabel);
        }

        public virtual void Release()
        {
            Released = true;
            Orphan();
        }

        protected void Accent(Color color, bool active)
        {
            Style.BorderColor = color;
            Style.BackgroundColor = active ? Color.FromHex("#272117") : CMUMedicalMachineStyle.DeepCardBg;
            var border = active ? new Thickness(2) : new Thickness(1);
            if (Style.BorderThickness != border)
            {
                Style.BorderThickness = border;
                InvalidateMeasure();
            }
        }
    }

    private sealed class QueueRow : AutodocRow
    {
        private readonly ulong _id;
        private Action<ulong>? _remove;
        private readonly Label _number;
        private readonly Button _button;

        public QueueRow(ulong id, Action<ulong> remove) : base(CMUMedicalMachineStyle.Blue)
        {
            _id = id;
            _remove = remove;
            _number = new Label { MinWidth = 26, Align = Label.AlignMode.Center,
                VerticalAlignment = VAlignment.Center, StyleClasses = { "LabelKeyText" } };
            Row.AddChild(_number);
            _number.SetPositionInParent(0);
            _button = new Button { Text = Loc.GetString("cmu-autodoc-remove-button"), MinWidth = 72,
                VerticalAlignment = VAlignment.Center };
            _button.OnPressed += _ => { if (!Released) _remove?.Invoke(_id); };
            Row.AddChild(_button);
        }

        public void Update(CMUAutodocQueueEntry entry, CMUAutodocBuiState state)
        {
            var active = entry.Index == 0 && state.Running;
            var accent = active ? CMUMedicalMachineStyle.Warning : CMUMedicalMachineStyle.Blue;
            Accent(accent, active);
            _number.FontColorOverride = accent;
            SetText(_number, (entry.Index + 1).ToString());
            SetText(TitleLabel, $"{entry.SurgeryDisplayName} - {entry.PartDisplayName}");
            SetText(DetailLabel, Loc.GetString("cmu-autodoc-procedure-time-note", ("time", FormatDuration(entry.DurationSeconds))));
            DetailLabel.FontColorOverride = active ? CMUMedicalMachineStyle.Warning : CMUMedicalMachineStyle.Muted;
            _button.Disabled = !state.CanQueue || state.CommandContext == null;
        }

        public override void Release() { _remove = null; base.Release(); }
    }

    private sealed class PartRow : AutodocRow
    {
        private readonly CMUSurgeryPartKey _key;
        private Action<CMUSurgeryPartKey>? _select;
        private readonly Button _button;

        public PartRow(CMUSurgeryPartKey key, Action<CMUSurgeryPartKey> select) : base(CMUMedicalMachineStyle.Cyan)
        {
            _key = key;
            _select = select;
            MinHeight = 52;
            // The button covers the row; keeping its text children retains focus and layout.
            RemoveChild(Row);
            _button = new Button { HorizontalExpand = true };
            _button.AddChild(Row);
            AddChild(_button);
            _button.OnPressed += _ => { if (!Released) _select?.Invoke(_key); };
        }

        public void Update(CMUSurgeryPartEntry part, bool selected)
        {
            Accent(selected ? CMUMedicalMachineStyle.Warning : part.EligibleSurgeries.Count > 0
                ? CMUMedicalMachineStyle.Cyan : CMUMedicalMachineStyle.Dim, selected);
            _button.ModulateSelfOverride = selected ? Color.White : Color.FromHex("#CDD6DE");
            SetText(TitleLabel, part.DisplayName);
            SetText(DetailLabel, part.EligibleSurgeries.Count == 0
                ? Loc.GetString("cmu-medical-surgery-part-condition-no-eligible")
                : Loc.GetString("cmu-autodoc-part-procedures", ("count", part.EligibleSurgeries.Count)));
        }

        public override void Release() { _select = null; base.Release(); }
    }

    private sealed class ProcedureRow : AutodocRow
    {
        private readonly CMUSurgeryPartKey _part;
        private readonly string _id;
        private Action<CMUSurgeryPartKey, string>? _queue;
        private readonly Button _button;

        public ProcedureRow(CMUSurgeryPartKey part, string id, Action<CMUSurgeryPartKey, string> queue)
            : base(CMUMedicalMachineStyle.Purple)
        {
            _part = part;
            _id = id;
            _queue = queue;
            _button = new Button { Text = Loc.GetString("cmu-autodoc-queue-button"), MinWidth = 86,
                VerticalAlignment = VAlignment.Center };
            _button.OnPressed += _ => { if (!Released) _queue?.Invoke(_part, _id); };
            Row.AddChild(_button);
        }

        public void Update(CMUSurgeryEntry surgery, CMUSurgeryPartEntry part, CMUAutodocBuiState state)
        {
            SetText(TitleLabel, surgery.DisplayName);
            SetText(DetailLabel, Loc.GetString("cmu-autodoc-procedure-time-note",
                ("time", surgery.AutodocDurationSeconds is { } duration ? FormatDuration(duration) : "-")));
            _button.Disabled = state.CommandContext == null || state.Queue.Count >= CMUAutodocPodComponent.MaximumQueueEntries ||
                state.Queue.Exists(entry => entry.Type == part.Type && entry.Symmetry == part.Symmetry && entry.SurgeryId == surgery.SurgeryId);
        }

        public override void Release() { _queue = null; base.Release(); }
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

    private static string FormatDuration(float seconds)
    {
        var total = Math.Max(0, (int) Math.Ceiling(seconds));
        return total < 60
            ? Loc.GetString("cmu-autodoc-seconds", ("seconds", total))
            : Loc.GetString("cmu-autodoc-minutes-seconds", ("minutes", total / 60), ("seconds", total % 60));
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
        SetText(PatientLabel, patientName);
        SetText(StatusLabel, status);
        SetText(CurrentStepLabel, currentStep);

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

    private static void SetText(Label label, string text)
    {
        if (!label.TextMemory.Span.SequenceEqual(text.AsSpan())) label.Text = text;
    }

    public void ScaleAddedControls(IReadOnlyList<Control> controls)
    {
        _uniformScaler.Apply(controls, _layoutScale, _resourceCache);
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
