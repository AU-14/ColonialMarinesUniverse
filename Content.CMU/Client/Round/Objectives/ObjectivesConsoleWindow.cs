using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.Round.Objectives;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Round.Objectives;

/// <summary>
///     The objectives console in the CRT theme: win points toward the final objective up top, then the objectives
///     grouped into final, major and minor, one card each. Every line of text wraps, so long objective names and
///     progress notes stay inside the window.
/// </summary>
public sealed class ObjectivesConsoleWindow : DefaultWindow
{
    public Action<string>? RequestIntelCallback;

    private readonly CrtConsoleKit _kit = new();

    private readonly Label _subheading;
    private readonly PanelContainer _pointsTag;
    private readonly Label _pointsText;
    private readonly Label _current;
    private readonly Label _required;
    private readonly Label _remaining;
    private readonly ProgressBar _progress;
    private readonly BoxContainer _list;
    private readonly Label _footer;

    public ObjectivesConsoleWindow()
    {
        Title = Loc.GetString("cmu-ui-objcon-title");
        MinSize = new Vector2(560, 480);
        SetSize = new Vector2(720, 680);

        _pointsTag = _kit.Tag(out _pointsText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-ui-objcon-heading"), out _subheading, _pointsTag));

        // Win points toward the final objective.
        var readouts = CrtConsoleKit.Row(6);
        readouts.AddChild(_kit.Readout(Loc.GetString("cmu-ui-objcon-current"), "0", out _current, CrtTerminalPalette.Accent));
        readouts.AddChild(_kit.Readout(Loc.GetString("cmu-ui-objcon-required"), "0", out _required));
        readouts.AddChild(_kit.Readout(Loc.GetString("cmu-ui-objcon-remaining"), "0", out _remaining, CrtTerminalPalette.Caution));
        _kit.Root.AddChild(readouts);

        _progress = new ProgressBar
        {
            MinHeight = 8,
            MinValue = 0,
            MaxValue = 1,
            HorizontalExpand = true,
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Accent },
        };
        _kit.Root.AddChild(_progress);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _list = CrtConsoleKit.Column(8);
        _list.HorizontalExpand = true;
        _list.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(_list);
        _kit.Root.AddChild(scroll);

        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.Text = Loc.GetString("cmu-ui-objcon-footer");

        Contents.AddChild(_kit.Tube);
    }

    public void UpdateObjectives(List<ObjectiveEntry> objectives, float currentWinPoints, int requiredWinPoints)
    {
        var done = objectives.Count(o => o.Status is ObjectiveStatusDisplay.Completed or ObjectiveStatusDisplay.Captured);
        var open = objectives.Count(o => o.Status is ObjectiveStatusDisplay.Uncompleted or ObjectiveStatusDisplay.Uncaptured
            or ObjectiveStatusDisplay.Repeating);
        _subheading.Text = Loc.GetString("cmu-ui-objcon-subheading",
            ("done", done), ("total", objectives.Count), ("open", open));

        var reached = requiredWinPoints > 0 && currentWinPoints >= requiredWinPoints;
        CrtConsoleKit.SetTag(_pointsTag, _pointsText,
            Loc.GetString("cmu-ui-objcon-points", ("points", Points(currentWinPoints)), ("required", requiredWinPoints)),
            reached ? CrtTerminalPalette.Accent : CrtTerminalPalette.Caution);

        _current.Text = Points(currentWinPoints);
        _required.Text = requiredWinPoints.ToString();
        _remaining.Text = Points(Math.Max(0, requiredWinPoints - currentWinPoints));
        _progress.Value = requiredWinPoints > 0 ? Math.Clamp(currentWinPoints / requiredWinPoints, 0f, 1f) : 0f;

        _list.RemoveAllChildren();
        if (objectives.Count == 0)
        {
            var none = CrtConsoleKit.Wrapping(Loc.GetString("cmu-ui-objcon-empty"), CrtTerminalPalette.TextDim);
            none.Margin = new Thickness(0, 24, 0, 0);
            _list.AddChild(none);
            return;
        }

        foreach (var group in objectives.GroupBy(o => o.Type).OrderBy(g => TypeRank(g.Key)))
        {
            var section = _kit.Section(Loc.GetString(TypeLoc(group.Key)), out var column, TypeColor(group.Key),
                _kit.Chip(group.Count().ToString(), CrtTerminalPalette.TextDim));
            foreach (var objective in group.OrderBy(o => StatusRank(o.Status)))
                column.AddChild(Card(objective));
            _list.AddChild(section);
        }
    }

    private Control Card(ObjectiveEntry objective)
    {
        var color = StatusColor(objective.Status);
        var finished = objective.Status is ObjectiveStatusDisplay.Completed or ObjectiveStatusDisplay.Failed
            or ObjectiveStatusDisplay.Captured;

        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
        };
        var row = CrtConsoleKit.Row(8);
        row.AddChild(new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
        });

        var column = CrtConsoleKit.Column(4);
        column.HorizontalExpand = true;
        column.Margin = new Thickness(0, 6, 8, 6);

        column.AddChild(CrtConsoleKit.WrappingMarkup(objective.Description,
            finished ? CrtTerminalPalette.TextDim : CrtTerminalPalette.TextBright));

        var details = Details(objective);
        if (details.Count > 0)
            column.AddChild(CrtConsoleKit.Wrapping(string.Join("  ·  ", details), CrtTerminalPalette.TextDim));

        var actions = CrtConsoleKit.Row(6);
        actions.AddChild(_kit.Chip(Loc.GetString(StatusLoc(objective.Status)), color));
        if (objective.Points > 0)
            actions.AddChild(_kit.Chip(Loc.GetString("cmu-ui-objcon-worth", ("points", Points(objective.Points))), CrtTerminalPalette.Caution));
        actions.AddChild(new Control { HorizontalExpand = true });
        var intel = _kit.Button(Loc.GetString("cmu-ui-objcon-intel"), CmuButtonStyles.Variant.Neutral);
        intel.MinWidth = 90;
        var id = objective.Id;
        intel.OnPressed += _ => RequestIntelCallback?.Invoke(id);
        actions.AddChild(intel);
        column.AddChild(actions);

        row.AddChild(column);
        panel.AddChild(row);
        return panel;
    }

    private static List<string> Details(ObjectiveEntry objective)
    {
        var details = new List<string>();
        if (objective.Repeating && objective.Status != ObjectiveStatusDisplay.Repeating)
            details.Add(Loc.GetString("cmu-ui-objcon-repeating"));
        if (!string.IsNullOrEmpty(objective.Progress))
            details.Add(objective.Progress);
        if (objective.MaxRepeatable is { } max and > 0)
        {
            details.Add(objective.RepeatsCompleted is { } repeats
                ? Loc.GetString("cmu-ui-objcon-repeats", ("done", repeats), ("max", max))
                : Loc.GetString("cmu-ui-objcon-repeats-max", ("max", max)));
        }
        else if (objective.RepeatsCompleted is { } only and > 0)
        {
            details.Add(Loc.GetString("cmu-ui-objcon-repeats-count", ("done", only)));
        }

        return details;
    }

    private static string Points(float points)
    {
        return points.ToString("0.##");
    }

    private static int TypeRank(ObjectiveTypeDisplay type) => type switch
    {
        ObjectiveTypeDisplay.Win => 0,
        ObjectiveTypeDisplay.Major => 1,
        _ => 2,
    };

    private static string TypeLoc(ObjectiveTypeDisplay type) => type switch
    {
        ObjectiveTypeDisplay.Win => "cmu-ui-objcon-type-final",
        ObjectiveTypeDisplay.Major => "cmu-ui-objcon-type-major",
        _ => "cmu-ui-objcon-type-minor",
    };

    private static Color TypeColor(ObjectiveTypeDisplay type) => type switch
    {
        ObjectiveTypeDisplay.Win => CrtTerminalPalette.TextBright,
        ObjectiveTypeDisplay.Major => CrtTerminalPalette.Accent,
        _ => CrtTerminalPalette.TextDim,
    };

    private static int StatusRank(ObjectiveStatusDisplay status) => status switch
    {
        ObjectiveStatusDisplay.Uncompleted => 0,
        ObjectiveStatusDisplay.Uncaptured => 0,
        ObjectiveStatusDisplay.Repeating => 1,
        _ => 2,
    };

    private static string StatusLoc(ObjectiveStatusDisplay status) => status switch
    {
        ObjectiveStatusDisplay.Completed => "cmu-ui-objcon-status-completed",
        ObjectiveStatusDisplay.Failed => "cmu-ui-objcon-status-failed",
        ObjectiveStatusDisplay.Captured => "cmu-ui-objcon-status-captured",
        ObjectiveStatusDisplay.Uncaptured => "cmu-ui-objcon-status-uncaptured",
        ObjectiveStatusDisplay.Repeating => "cmu-ui-objcon-status-repeating",
        _ => "cmu-ui-objcon-status-open",
    };

    private static Color StatusColor(ObjectiveStatusDisplay status) => status switch
    {
        ObjectiveStatusDisplay.Completed => CrtTerminalPalette.Accent,
        ObjectiveStatusDisplay.Captured => CrtTerminalPalette.Accent,
        ObjectiveStatusDisplay.Failed => CrtTerminalPalette.Alert,
        ObjectiveStatusDisplay.Repeating => CrtTerminalPalette.TextBright,
        _ => CrtTerminalPalette.Caution,
    };
}
