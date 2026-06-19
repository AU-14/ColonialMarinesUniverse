using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;

namespace Content.Client._CMU14.Medical.Surgery;

[UsedImplicitly]
public sealed partial class CMUSurgeryBui : BoundUserInterface
{
    [Dependency] private ILocalizationManager _localization = default!;

    private static readonly Color RowBackground = Color.FromHex("#1C1D23").WithAlpha(0.92f);
    private static readonly Color RowEmptyBackground = Color.FromHex("#17191E").WithAlpha(0.86f);
    private static readonly Color AccentBlue = Color.FromHex("#B8A06A");
    private static readonly Color MutedBorder = Color.FromHex("#464854");
    private static readonly Color TextPrimary = Color.FromHex("#F2EEE7");
    private static readonly Color TextSecondary = Color.FromHex("#B7B0A5");
    private static readonly Color TextDim = Color.FromHex("#77736D");
    private static readonly Color Warning = Color.FromHex("#D78954");
    private static readonly Color Active = Color.FromHex("#D2A95D");
    private static readonly Color Danger = Color.FromHex("#C95B56");
    private static readonly Color Healthy = Color.FromHex("#7FA174");

    [ViewVariables]
    private CMUSurgeryWindow? _window;

    // Missing limb slots share the patient NetEntity, so type/symmetry are
    // part of the key.
    private (NetEntity Part, BodyPartType Type, BodyPartSymmetry Symmetry)? _selectedPart;

    public CMUSurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUSurgeryWindow>();
        _window.Title = Loc.GetString("cmu-medical-surgery-window-title");
        if (State is CMUSurgeryBuiState s)
            Refresh(s);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CMUSurgeryBuiState s)
            Refresh(s);
    }

    private void Refresh(CMUSurgeryBuiState state)
    {
        if (_window is null)
            return;

        _window.PatientLabel.Text = string.IsNullOrEmpty(state.PatientName)
            ? Loc.GetString("cmu-medical-surgery-window-title")
            : state.PatientName;

        _window.WorkflowStatusLabel.Text = state.InFlight is { } inFlight
            ? Loc.GetString(
                "cmu-medical-surgery-workflow-active",
                ("surgery", inFlight.LeafSurgeryDisplayName),
                ("part", inFlight.PartDisplayName))
            : Loc.GetString("cmu-medical-surgery-workflow-ready");

        RefreshInProgressPanel(state);
        RefreshPartStack(state);
        _window.ApplyUniformScale(true);
    }

    private void RefreshInProgressPanel(CMUSurgeryBuiState state)
    {
        if (_window is null)
            return;

        var armed = state.CurrentArmedStep;
        var inFlight = state.InFlight;

        if (inFlight is null && armed is null)
        {
            _window.InProgressPanel.Visible = false;
            _window.HintPanel.Visible = true;
            _window.InProgressActionRailPanel.Visible = false;
            _window.InProgressChoiceContainer.DisposeAllChildren();
            return;
        }

        _window.InProgressPanel.Visible = true;
        _window.HintPanel.Visible = false;
        _window.InProgressActionRailPanel.Visible = false;

        var subtitle = inFlight is not null
            ? Loc.GetString("cmu-medical-surgery-in-progress-subtitle",
                ("surgery", inFlight.LeafSurgeryDisplayName),
                ("part", inFlight.PartDisplayName))
            : armed?.SurgeryDisplayName ?? string.Empty;
        _window.InProgressSubtitleLabel.Text = subtitle;

        if (inFlight is not null)
        {
            var elapsed = FormatElapsedFromTimestamp(inFlight.StartedAt);
            _window.InProgressCreditLabel.Text = Loc.GetString(
                "cmu-medical-surgery-in-progress-credit",
                ("surgeon", string.IsNullOrEmpty(inFlight.SurgeonName) ? "-" : inFlight.SurgeonName),
                ("elapsed", elapsed));
            _window.InProgressCreditLabel.Visible = true;
        }
        else
        {
            _window.InProgressCreditLabel.Visible = false;
        }

        if (armed is not null)
        {
            var stepLabel = ResolveLabel(armed.StepLabel);
            _window.InProgressStepLabel.Text = Loc.GetString(
                "cmu-medical-surgery-step-now",
                ("step", armed.StepIndex + 1),
                ("label", stepLabel));

            var tool = FormatToolCategory(armed.ToolCategory);
            var partName = inFlight?.PartDisplayName ?? string.Empty;
            _window.InProgressActionLabel.Text = string.IsNullOrEmpty(armed.ToolCategory)
                ? Loc.GetString("cmu-medical-surgery-action-hint-no-tool", ("part", partName))
                : Loc.GetString("cmu-medical-surgery-action-hint", ("part", partName), ("tool", tool));
            _window.InProgressStepLabel.Visible = true;
            _window.InProgressActionLabel.Visible = true;
        }
        else if (inFlight is not null && TryGetInFlightEntry(state, inFlight, out var next))
        {
            var stepLabel = ResolveLabel(next.NextStepLabel);
            _window.InProgressStepLabel.Text = Loc.GetString(
                "cmu-medical-surgery-step-now",
                ("step", next.NextStepIndex + 1),
                ("label", stepLabel));

            var tool = FormatToolCategory(next.NextStepToolCategory);
            _window.InProgressActionLabel.Text = string.IsNullOrEmpty(next.NextStepToolCategory)
                ? Loc.GetString("cmu-medical-surgery-action-hint-no-tool", ("part", inFlight.PartDisplayName))
                : Loc.GetString("cmu-medical-surgery-action-hint", ("part", inFlight.PartDisplayName), ("tool", tool));
            _window.InProgressStepLabel.Visible = true;
            _window.InProgressActionLabel.Visible = true;
        }
        else
        {
            _window.InProgressStepLabel.Visible = false;
            _window.InProgressActionLabel.Visible = false;
        }

        RefreshInProgressChoices(state, inFlight, armed is not null);
    }

    private void RefreshInProgressChoices(CMUSurgeryBuiState state, CMUSurgeryInFlightInfo? inFlight, bool stepArmed)
    {
        if (_window is null)
            return;

        _window.InProgressChoiceContainer.DisposeAllChildren();
        if (inFlight is null || !TryGetInFlightPart(state, inFlight, out var part))
        {
            _window.InProgressActionRailPanel.Visible = true;
            AddAbandonButton();
            return;
        }

        _window.InProgressActionRailPanel.Visible = true;

        var continuationCount = 0;
        CMUSurgeryEntry? closeUp = null;
        if (!stepArmed)
        {
            foreach (var entry in part.EligibleSurgeries)
            {
                if (entry.Category == "close_up")
                {
                    closeUp ??= entry;
                    continue;
                }

                if (entry.SurgeryId == inFlight.LeafSurgeryId)
                    continue;

                if (!ShouldShowInProgressContinuation(entry))
                    continue;

                AddChoiceButton(
                    part,
                    entry,
                    Loc.GetString("cmu-medical-surgery-continue-with-button", ("surgery", entry.DisplayName)),
                    false);
                continuationCount++;
            }

            if (continuationCount > 0)
            {
                _window.InProgressStepLabel.Visible = true;
                _window.InProgressActionLabel.Visible = true;
                _window.InProgressStepLabel.Text = Loc.GetString("cmu-medical-surgery-choose-next-heading");
                _window.InProgressActionLabel.Text = Loc.GetString("cmu-medical-surgery-choose-next-hint");
            }

            if (closeUp is { } close)
                AddChoiceButton(part, close, Loc.GetString("cmu-medical-surgery-close-up-button"), true);
        }

        AddAbandonButton();
    }

    private static bool ShouldShowInProgressContinuation(CMUSurgeryEntry entry)
    {
        return entry.Category is "bleed"
            or "fracture"
            or "burn"
            or "parasite"
            or "suture"
            or "head_organ"
            or "transplant";
    }

    private void AddAbandonButton()
    {
        if (_window is null)
            return;

        var abandonButton = CreateActionButton(
            Loc.GetString("cmu-medical-surgery-abandon-button"),
            MutedBorder,
            new Thickness(0, 5, 0, 0));
        abandonButton.OnPressed += _ => SendMessage(new CMUSurgeryClearArmedMessage());
        _window.InProgressChoiceContainer.AddChild(abandonButton);
    }

    private void AddChoiceButton(CMUSurgeryPartEntry part, CMUSurgeryEntry entry, string text, bool closeUp)
    {
        if (_window is null)
            return;

        var capturedPart = part;
        var capturedEntry = entry;
        var button = CreateActionButton(text, closeUp ? Active : GetCategoryColor(entry.Category), new Thickness(0));
        button.OnPressed += _ => SendMessage(new CMUSurgeryArmStepMessage(
            capturedPart.Part,
            capturedPart.Type,
            capturedPart.Symmetry,
            capturedEntry.SurgeryId,
            capturedEntry.NextStepIndex));
        _window.InProgressChoiceContainer.AddChild(button);
    }

    private Button CreateActionButton(string text, Color tint, Thickness margin)
    {
        var button = new Button
        {
            Text = text,
            StyleClasses = { "OpenBoth" },
            HorizontalAlignment = Control.HAlignment.Stretch,
            HorizontalExpand = true,
            MinWidth = 160,
            Margin = margin,
        };
        button.ModulateSelfOverride = tint;
        return button;
    }

    private static bool TryGetInFlightEntry(
        CMUSurgeryBuiState state,
        CMUSurgeryInFlightInfo inFlight,
        out CMUSurgeryEntry entry)
    {
        if (!TryGetInFlightPart(state, inFlight, out var part))
        {
            entry = default!;
            return false;
        }

        foreach (var candidate in part.EligibleSurgeries)
        {
            if (candidate.SurgeryId != inFlight.LeafSurgeryId)
                continue;

            entry = candidate;
            return true;
        }

        entry = default!;
        return false;
    }

    private static bool TryGetInFlightPart(
        CMUSurgeryBuiState state,
        CMUSurgeryInFlightInfo inFlight,
        out CMUSurgeryPartEntry entry)
    {
        foreach (var part in state.Parts)
        {
            if (!part.IsInFlightHere && part.Part != inFlight.Part)
                continue;

            entry = part;
            return true;
        }

        entry = default!;
        return false;
    }

    private void RefreshPartStack(CMUSurgeryBuiState state)
    {
        if (_window is null)
            return;

        EnsureSelectedPart(state);

        _window.PartListContainer.DisposeAllChildren();
        _window.ProcedureListContainer.DisposeAllChildren();

        if (state.Parts.Count == 0)
        {
            _window.SelectedPartLabel.Text = Loc.GetString("cmu-medical-surgery-section-surgeries");
            _window.SelectedPartStatusLabel.Text = Loc.GetString("cmu-medical-surgery-no-eligible");
            _window.ProcedureHeaderLabel.Text = string.Empty;
            _window.PartListContainer.AddChild(CreateEmptyLabel(Loc.GetString("cmu-medical-surgery-no-eligible")));
            _window.ProcedureListContainer.AddChild(CreateEmptyLabel(Loc.GetString("cmu-medical-surgery-no-eligible")));
            _window.ApplyUniformScale(true);
            return;
        }

        foreach (var part in state.Parts)
        {
            var selected = _selectedPart is { } key && PartMatches(part, key);
            _window.PartListContainer.AddChild(BuildPartListButton(part, selected));
        }

        if (!TryGetSelectedPart(state, out var selectedPart))
        {
            _window.SelectedPartLabel.Text = Loc.GetString("cmu-medical-surgery-section-surgeries");
            _window.SelectedPartStatusLabel.Text = Loc.GetString("cmu-medical-surgery-no-part-selected");
            _window.ProcedureHeaderLabel.Text = string.Empty;
            _window.ProcedureListContainer.AddChild(CreateEmptyLabel(Loc.GetString("cmu-medical-surgery-no-part-selected")));
            _window.ApplyUniformScale(true);
            return;
        }

        BuildProcedurePanel(selectedPart);
        _window.ApplyUniformScale(true);
    }

    private void EnsureSelectedPart(CMUSurgeryBuiState state)
    {
        if (state.Parts.Count == 0)
        {
            _selectedPart = null;
            return;
        }

        if (_selectedPart is { } selected && ContainsPart(state, selected))
            return;

        foreach (var part in state.Parts)
        {
            if (part.IsInFlightHere)
            {
                _selectedPart = GetPartKey(part);
                return;
            }
        }

        foreach (var part in state.Parts)
        {
            if (!part.LockedByOtherPart && part.EligibleSurgeries.Count > 0)
            {
                _selectedPart = GetPartKey(part);
                return;
            }
        }

        _selectedPart = GetPartKey(state.Parts[0]);
    }

    private static bool ContainsPart(CMUSurgeryBuiState state, (NetEntity Part, BodyPartType Type, BodyPartSymmetry Symmetry) key)
    {
        foreach (var part in state.Parts)
        {
            if (PartMatches(part, key))
                return true;
        }

        return false;
    }

    private bool TryGetSelectedPart(CMUSurgeryBuiState state, out CMUSurgeryPartEntry part)
    {
        if (_selectedPart is { } key)
        {
            foreach (var candidate in state.Parts)
            {
                if (!PartMatches(candidate, key))
                    continue;

                part = candidate;
                return true;
            }
        }

        part = default!;
        return false;
    }

    private Control BuildPartListButton(CMUSurgeryPartEntry part, bool selected)
    {
        var captured = part;
        var status = ResolveStatusText(part);

        var button = new Button
        {
            StyleClasses = { "OpenBoth" },
            HorizontalExpand = true,
        };
        button.OnPressed += _ =>
        {
            _selectedPart = GetPartKey(captured);
            if (State is CMUSurgeryBuiState s)
                RefreshPartStack(s);
        };

        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            MinSize = new Vector2(0, 64),
            PanelOverride = CreatePanelStyle(
                selected ? Color.FromHex("#24202C").WithAlpha(0.94f) : part.EligibleSurgeries.Count > 0 ? RowBackground : RowEmptyBackground,
                selected ? AccentBlue : part.IsInFlightHere ? Active : MutedBorder,
                selected ? 2f : 1f),
        };
        button.AddChild(panel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 9,
            Margin = new Thickness(7),
            HorizontalExpand = true,
        };
        panel.AddChild(root);

        root.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 50),
            PanelOverride = CreatePanelStyle(GetPartAccent(part), GetPartAccent(part), 0f),
        });

        var labels = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 1,
            HorizontalExpand = true,
        };
        labels.AddChild(new Label
        {
            Text = part.DisplayName,
            FontColorOverride = selected ? TextPrimary : Color.FromHex("#D7D0C5"),
            ClipText = true,
        });
        labels.AddChild(new Label
        {
            Text = status.Text,
            FontColorOverride = status.Color,
            ClipText = true,
        });
        root.AddChild(labels);

        return button;
    }

    private void BuildProcedurePanel(CMUSurgeryPartEntry part)
    {
        if (_window is null)
            return;

        var status = ResolveStatusText(part);
        _window.SelectedPartLabel.Text = part.DisplayName;
        _window.SelectedPartStatusLabel.Text = status.Text;
        _window.SelectedPartStatusLabel.FontColorOverride = status.Color;
        _window.ProcedureHeaderLabel.Text = Loc.GetString("cmu-medical-surgery-section-surgeries-on", ("part", part.DisplayName));

        if (part.LockedByOtherPart)
        {
            _window.ProcedureListContainer.AddChild(CreateEmptyLabel(
                Loc.GetString("cmu-medical-surgery-part-condition-locked", ("other", part.DisplayName))));
            return;
        }

        if (part.EligibleSurgeries.Count == 0)
        {
            _window.ProcedureListContainer.AddChild(CreateEmptyLabel(
                Loc.GetString("cmu-medical-surgery-part-condition-no-eligible")));
            return;
        }

        var groups = new List<(string Category, List<CMUSurgeryEntry> Entries)>();
        foreach (var entry in part.EligibleSurgeries)
        {
            var index = groups.FindIndex(g => g.Category == entry.Category);
            if (index < 0)
                groups.Add((entry.Category, new List<CMUSurgeryEntry> { entry }));
            else
                groups[index].Entries.Add(entry);
        }

        MoveCategoryToEnd(groups, "remove_organ");

        foreach (var (category, entries) in groups)
        {
            _window.ProcedureListContainer.AddChild(new Label
            {
                Text = ResolveCategoryName(category),
                StyleClasses = { "LabelHeading" },
                FontColorOverride = GetCategoryColor(category),
                Margin = new Thickness(0, 2, 0, 0),
            });

            foreach (var entry in entries)
            {
                _window.ProcedureListContainer.AddChild(BuildSurgeryRow(part, entry));
            }
        }
    }

    private static void MoveCategoryToEnd(List<(string Category, List<CMUSurgeryEntry> Entries)> groups, string category)
    {
        var index = groups.FindIndex(g => g.Category == category);
        if (index < 0 || index == groups.Count - 1)
            return;

        var group = groups[index];
        groups.RemoveAt(index);
        groups.Add(group);
    }

    private Control BuildSurgeryRow(CMUSurgeryPartEntry part, CMUSurgeryEntry entry)
    {
        var captured = entry;
        var partCaptured = part;
        var isCloseUp = entry.Category == "close_up";
        var categoryColor = isCloseUp ? Active : GetCategoryColor(entry.Category);

        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            MinSize = new Vector2(0, 58),
            PanelOverride = CreatePanelStyle(
                GetCategoryRowBackground(entry.Category),
                categoryColor,
                IsHighAttentionCategory(entry.Category) ? 2f : 1.25f),
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(6),
            HorizontalExpand = true,
        };
        panel.AddChild(root);

        var accent = new PanelContainer
        {
            MinSize = new Vector2(7, 44),
            PanelOverride = CreatePanelStyle(categoryColor, categoryColor, 0f),
        };

        var labels = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true,
        };
        labels.AddChild(new Label
        {
            Text = entry.DisplayName,
            FontColorOverride = TextPrimary,
            ClipText = true,
        });
        labels.AddChild(new Label
        {
            Text = Loc.GetString(
                "cmu-medical-surgery-procedure-detail",
                ("step", ResolveLabel(entry.NextStepLabel)),
                ("tool", FormatToolCategory(entry.NextStepToolCategory))),
            FontColorOverride = TextSecondary,
            ClipText = true,
        });

        root.AddChild(accent);
        root.AddChild(labels);

        var beginButton = CreateActionButton(
            part.IsInFlightHere
                ? Loc.GetString("cmu-medical-surgery-continue-button")
                : Loc.GetString("cmu-medical-surgery-arm-button"),
            categoryColor,
            new Thickness(0));
        beginButton.MinWidth = 140;
        beginButton.HorizontalExpand = false;
        beginButton.VerticalAlignment = Control.VAlignment.Center;
        beginButton.OnPressed += _ => SendMessage(
            new CMUSurgeryArmStepMessage(
                partCaptured.Part,
                partCaptured.Type,
                partCaptured.Symmetry,
                captured.SurgeryId,
                captured.NextStepIndex));
        root.AddChild(beginButton);

        return panel;
    }

    private static Control CreateEmptyLabel(string text)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = CreatePanelStyle(RowEmptyBackground, MutedBorder, 1f),
        };
        panel.AddChild(new Label
        {
            Text = text,
            FontColorOverride = TextSecondary,
            Margin = new Thickness(8, 6),
            ClipText = true,
        });
        return panel;
    }

    private static (NetEntity Part, BodyPartType Type, BodyPartSymmetry Symmetry) GetPartKey(CMUSurgeryPartEntry part)
    {
        return (part.Part, part.Type, part.Symmetry);
    }

    private static bool PartMatches(CMUSurgeryPartEntry part, (NetEntity Part, BodyPartType Type, BodyPartSymmetry Symmetry) key)
    {
        return part.Part == key.Part
            && part.Type == key.Type
            && part.Symmetry == key.Symmetry;
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color border, float borderThickness)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(borderThickness),
        };
    }

    private static Color GetPartAccent(CMUSurgeryPartEntry part)
    {
        if (part.IsInFlightHere)
            return Active;
        if (part.LockedByOtherPart)
            return TextDim;
        if (part.EligibleSurgeries.Count > 0)
            return AccentBlue;
        if (!string.IsNullOrEmpty(part.ConditionSummary))
            return Danger;

        return Healthy;
    }

    private static Color GetCategoryColor(string category)
    {
        return category switch
        {
            "fracture" => Color.FromHex("#E6C76C"),
            "bleed" => Danger,
            "burn" => Warning,
            "remove_organ" or "transplant" or "suture" or "head_organ" => Color.FromHex("#A98DCE"),
            "amputation" => Danger,
            "reattach" => Healthy,
            "parasite" => Color.FromHex("#D87968"),
            "close_up" => Active,
            _ => AccentBlue,
        };
    }

    private static Color GetCategoryRowBackground(string category)
    {
        return category switch
        {
            "close_up" => Color.FromHex("#282318").WithAlpha(0.94f),
            "bleed" => Color.FromHex("#25191B").WithAlpha(0.94f),
            "burn" => Color.FromHex("#251D17").WithAlpha(0.94f),
            "fracture" => Color.FromHex("#252217").WithAlpha(0.94f),
            "remove_organ" or "amputation" => Color.FromHex("#251819").WithAlpha(0.94f),
            "suture" or "head_organ" or "transplant" => Color.FromHex("#211C2A").WithAlpha(0.94f),
            "parasite" => Color.FromHex("#251A19").WithAlpha(0.94f),
            _ => RowBackground,
        };
    }

    private static bool IsHighAttentionCategory(string category)
    {
        return category is "bleed"
            or "remove_organ"
            or "amputation"
            or "close_up"
            or "parasite";
    }

    private string ResolveCategoryName(string category)
    {
        var key = "cmu-medical-surgery-category-" + category;
        return _localization.TryGetString(key, out var resolved) ? resolved : category;
    }

    private static (string Text, Color Color) ResolveStatusText(CMUSurgeryPartEntry part)
    {
        if (part.IsInFlightHere)
            return (Loc.GetString("cmu-medical-surgery-condition-in-progress"), Active);
        if (part.LockedByOtherPart)
            return (Loc.GetString("cmu-medical-surgery-part-condition-no-eligible"), TextDim);
        if (!string.IsNullOrEmpty(part.ConditionSummary))
            return (part.ConditionSummary, Danger);
        return (Loc.GetString("cmu-medical-surgery-part-condition-healthy"), Healthy);
    }

    private string ResolveLabel(string? maybeKey)
    {
        if (string.IsNullOrEmpty(maybeKey))
            return "-";
        if (_localization.TryGetString(maybeKey, out var resolved))
            return resolved;
        return maybeKey;
    }

    private string FormatToolCategory(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return "-";
        var key = "cmu-medical-surgery-tool-category-" + category;
        if (_localization.TryGetString(key, out var resolved))
            return resolved;
        return category;
    }

    private string FormatElapsedFromTimestamp(TimeSpan startedAt)
    {
        var timing = IoCManager.Resolve<Robust.Shared.Timing.IGameTiming>();
        var span = timing.CurTime - startedAt;
        if (span.TotalMinutes < 1)
            return $"{(int) span.TotalSeconds}s";
        if (span.TotalMinutes < 60)
            return $"{(int) span.TotalMinutes}m";
        return $"{(int) span.TotalHours}h{(int) (span.TotalMinutes % 60)}m";
    }
}
