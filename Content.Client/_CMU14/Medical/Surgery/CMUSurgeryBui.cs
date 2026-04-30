using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Medical.Surgery;

[UsedImplicitly]
public sealed class CMUSurgeryBui : BoundUserInterface
{
    [ViewVariables]
    private CMUSurgeryWindow? _window;

    // Severed limbs share the patient NetEntity, so keying by NetEntity
    // alone collapses both panels into one toggle.
    private readonly HashSet<(NetEntity, BodyPartType, BodyPartSymmetry)> _expanded = new();

    public CMUSurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUSurgeryWindow>();
        _window.Title = Loc.GetString("cmu-medical-surgery-window-title");
        _window.AbandonButton.OnPressed += _ => SendMessage(new CMUSurgeryClearArmedMessage());
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

        RefreshInProgressPanel(state);
        RefreshPartStack(state);
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
            return;
        }

        _window.InProgressPanel.Visible = true;
        _window.HintPanel.Visible = false;

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
                ("surgeon", string.IsNullOrEmpty(inFlight.SurgeonName) ? "—" : inFlight.SurgeonName),
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
                ("total", "?"),
                ("label", stepLabel));

            var tool = FormatToolCategory(armed.ToolCategory);
            var partName = inFlight?.PartDisplayName ?? "";
            _window.InProgressActionLabel.Text = string.IsNullOrEmpty(armed.ToolCategory)
                ? Loc.GetString("cmu-medical-surgery-action-hint-no-tool", ("part", partName))
                : Loc.GetString("cmu-medical-surgery-action-hint", ("part", partName), ("tool", tool));
            _window.InProgressStepLabel.Visible = true;
            _window.InProgressActionLabel.Visible = true;
        }
        else
        {
            _window.InProgressStepLabel.Visible = false;
            _window.InProgressActionLabel.Visible = false;
        }

        _window.AbandonButton.Visible = inFlight is not null || armed is not null;
    }

    private void RefreshPartStack(CMUSurgeryBuiState state)
    {
        if (_window is null)
            return;
        _window.PartStackContainer.DisposeAllChildren();

        if (state.Parts.Count == 0)
        {
            _window.PartStackContainer.AddChild(new Label
            {
                Text = Loc.GetString("cmu-medical-surgery-no-eligible"),
                Margin = new Thickness(0, 4),
            });
            return;
        }

        foreach (var part in state.Parts)
        {
            _window.PartStackContainer.AddChild(BuildPartPanel(part));
        }
    }

    private Control BuildPartPanel(CMUSurgeryPartEntry part)
    {
        var captured = part;
        var key = (part.Part, part.Type, part.Symmetry);
        var expanded = _expanded.Contains(key) || part.IsInFlightHere;

        var panel = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 4),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1B1F2A") },
        };
        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8, 6),
        };
        panel.AddChild(stack);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var caret = new Label
        {
            Text = expanded ? "▼ " : "▶ ",
            FontColorOverride = Color.FromHex("#B6BFCE"),
        };
        header.AddChild(caret);
        var name = new Label
        {
            Text = part.DisplayName,
            StyleClasses = { "LabelKeyText" },
        };
        header.AddChild(name);
        header.AddChild(new Control { HorizontalExpand = true });
        var statusText = ResolveStatusText(part);
        var statusLabel = new Label
        {
            Text = statusText.Text,
            FontColorOverride = statusText.Color,
        };
        header.AddChild(statusLabel);

        var headerButton = new Button
        {
            StyleClasses = { "OpenBoth" },
            HorizontalExpand = true,
        };
        headerButton.AddChild(header);
        headerButton.OnPressed += _ =>
        {
            if (_expanded.Contains(key))
                _expanded.Remove(key);
            else
                _expanded.Add(key);
            if (State is CMUSurgeryBuiState s)
                RefreshPartStack(s);
        };
        stack.AddChild(headerButton);

        if (expanded)
        {
            stack.AddChild(BuildPartBody(part));
        }

        return panel;
    }

    private Control BuildPartBody(CMUSurgeryPartEntry part)
    {
        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(20, 4, 0, 4),
        };

        if (part.LockedByOtherPart)
        {
            body.AddChild(new Label
            {
                Text = Loc.GetString("cmu-medical-surgery-part-condition-locked", ("other", part.DisplayName)),
                FontColorOverride = Color.FromHex("#888888"),
            });
            return body;
        }

        if (part.EligibleSurgeries.Count == 0)
        {
            body.AddChild(new Label
            {
                Text = Loc.GetString("cmu-medical-surgery-part-condition-no-eligible"),
                FontColorOverride = Color.FromHex("#888888"),
            });
            return body;
        }

        var groups = new List<(string Category, List<CMUSurgeryEntry> Entries)>();
        foreach (var entry in part.EligibleSurgeries)
        {
            var existing = groups.Find(g => g.Category == entry.Category);
            if (existing.Entries is null)
                groups.Add((entry.Category, new List<CMUSurgeryEntry> { entry }));
            else
                existing.Entries.Add(entry);
        }

        foreach (var (category, entries) in groups)
        {
            body.AddChild(new Label
            {
                Text = ResolveCategoryName(category),
                StyleClasses = { "LabelHeading" },
                FontColorOverride = Color.FromHex("#E0C97A"),
                Margin = new Thickness(0, 4, 0, 2),
            });

            foreach (var entry in entries)
            {
                var captured = entry;
                var partCaptured = part.Part;
                var partTypeCaptured = part.Type;
                var partSymmetryCaptured = part.Symmetry;
                var row = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(0, 2),
                };

                var labels = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
                labels.AddChild(new Label { Text = entry.DisplayName, StyleClasses = { "LabelKeyText" } });
                var sublineLabel = new Label
                {
                    Text = ResolveLabel(entry.NextStepLabel) + " · " + FormatToolCategory(entry.NextStepToolCategory),
                    FontColorOverride = Color.FromHex("#B6BFCE"),
                };
                labels.AddChild(sublineLabel);
                row.AddChild(labels);

                var beginButton = new Button
                {
                    Text = part.IsInFlightHere
                        ? Loc.GetString("cmu-medical-surgery-continue-button")
                        : Loc.GetString("cmu-medical-surgery-arm-button"),
                    StyleClasses = { "OpenBoth" },
                    MinWidth = 130,
                };
                beginButton.OnPressed += _ => SendMessage(
                    new CMUSurgeryArmStepMessage(partCaptured, partTypeCaptured, partSymmetryCaptured, captured.SurgeryId, captured.NextStepIndex));
                row.AddChild(beginButton);

                body.AddChild(row);
            }
        }

        return body;
    }

    private static string ResolveCategoryName(string category)
    {
        var key = "cmu-medical-surgery-category-" + category;
        return Loc.TryGetString(key, out var resolved) ? resolved : category;
    }

    private static (string Text, Color Color) ResolveStatusText(CMUSurgeryPartEntry part)
    {
        if (part.IsInFlightHere)
            return (Loc.GetString("cmu-medical-surgery-condition-in-progress"), Color.FromHex("#FFD56B"));
        if (!string.IsNullOrEmpty(part.ConditionSummary))
            return (part.ConditionSummary, Color.FromHex("#E07070"));
        if (part.LockedByOtherPart)
            return (Loc.GetString("cmu-medical-surgery-part-condition-no-eligible"), Color.FromHex("#888888"));
        return (Loc.GetString("cmu-medical-surgery-part-condition-healthy"), Color.FromHex("#7AC97A"));
    }

    private static string ResolveLabel(string? maybeKey)
    {
        if (string.IsNullOrEmpty(maybeKey))
            return "—";
        if (Loc.TryGetString(maybeKey, out var resolved))
            return resolved;
        return maybeKey;
    }

    private static string FormatToolCategory(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return "—";
        var key = "cmu-medical-surgery-tool-category-" + category;
        if (Loc.TryGetString(key, out var resolved))
            return resolved;
        return category;
    }

    private string FormatElapsedFromTimestamp(TimeSpan startedAt)
    {
        var timing = IoCManager.Resolve<Robust.Shared.Timing.IGameTiming>();
        var span = timing.CurTime - startedAt;
        if (span.TotalMinutes < 1)
            return $"{(int)span.TotalSeconds}s";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes}m";
        return $"{(int)span.TotalHours}h{(int)(span.TotalMinutes % 60)}m";
    }
}
