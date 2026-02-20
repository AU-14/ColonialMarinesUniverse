using Content.Shared._RMC14.Intel;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Linq;

namespace Content.Client._RMC14.Intel;

[UsedImplicitly]
public sealed class ViewIntelObjectivesBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ViewIntelObjectivesWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ViewIntelObjectivesWindow>();
        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out ViewIntelObjectivesComponent? comp))
            return;

        // If TeamTrees is populated (ghost viewing), display all teams' values. Otherwise display single team tree.
        if (comp.TeamTrees.Count > 0)
        {
            // Header: points and tiers per team
            var pointsLines = comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + kvp.Value.Points.Double().ToString("F1") + " pts");
            _window.CurrentPointsLabel.Text = string.Join("\n", pointsLines.ToArray());

            var tierLines = comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": Tier " + kvp.Value.Tier);
            _window.CurrentTierLabel.Text = string.Join("\n", tierLines.ToArray());

            // Total earned per team
            var totalLines = comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + kvp.Value.TotalEarned.Double().ToString("F1") + " pts");
            _window.TotalPointsLabel.Text = string.Join("\n", totalLines.ToArray());

            // Objectives: show per-team lines for each objective
            _window.DocumentsLabel.Text = string.Join("\n", comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + kvp.Value.Documents.Current + "/" + kvp.Value.Documents.Total).ToArray());
            _window.RetrieveItemsLabel.Text = string.Join("\n", comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + kvp.Value.RetrieveItems.Current + "/" + kvp.Value.RetrieveItems.Total).ToArray());
            _window.RescueSurvivorsLabel.Text = string.Join("\n", comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + kvp.Value.RescueSurvivors).ToArray());
            _window.RecoverCorpsesLabel.Text = string.Join("\n", comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + kvp.Value.RecoverCorpses).ToArray());
            _window.ColonyCommunicationsLabel.Text = string.Join("\n", comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + (kvp.Value.ColonyCommunications ? Loc.GetString("rmc-ui-intel-colony-status-online") : Loc.GetString("rmc-ui-intel-colony-status-offline"))).ToArray());
            _window.ColonyPowerLabel.Text = string.Join("\n", comp.TeamTrees.Select(kvp => kvp.Key.ToUpperInvariant() + ": " + (kvp.Value.ColonyPower ? Loc.GetString("rmc-ui-intel-colony-status-online") : Loc.GetString("rmc-ui-intel-colony-status-offline"))).ToArray());

            // Clues: create tabs for each team and category
            _window.CluesContainer.DisposeAllChildren();
            foreach (var (team, teamTree) in comp.TeamTrees)
            {
                foreach (var (category, clues) in teamTree.Clues)
                {
                    var scroll = new ScrollContainer
                    {
                        HScrollEnabled = false,
                        VScrollEnabled = true,
                    };

                    var container = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        Margin = new Thickness(4),
                    };

                    foreach (var (_, clue) in clues)
                    {
                        container.AddChild(new Label
                        {
                            Text = clue,
                            Margin = new Thickness(2, 1, 2, 1),
                            StyleClasses = { "Label" }
                        });
                    }

                    scroll.AddChild(container);
                    _window.CluesContainer.AddChild(scroll);
                    TabContainer.SetTabTitle(scroll, $"{team.ToUpperInvariant()} - {Loc.GetString(category)}");
                }
            }

            return;
        }

        // Default single-tree display
        var tree = comp.Tree;
        _window.CurrentPointsLabel.Text = Loc.GetString("rmc-ui-intel-points-value", ("value", tree.Points.Double().ToString("F1")));
        _window.CurrentTierLabel.Text = Loc.GetString("rmc-ui-intel-tier-value", ("value", tree.Tier));
        _window.TotalPointsLabel.Text = Loc.GetString("rmc-ui-intel-total-credits", ("value", tree.TotalEarned.Double().ToString("F1")));
        _window.DocumentsLabel.Text = Loc.GetString("rmc-ui-intel-progress", ("current", tree.Documents.Current), ("total", tree.Documents.Total));
        _window.RetrieveItemsLabel.Text = Loc.GetString("rmc-ui-intel-progress", ("current", tree.RetrieveItems.Current), ("total", tree.RetrieveItems.Total));
        _window.RescueSurvivorsLabel.Text = Loc.GetString("rmc-ui-intel-infinite-progress", ("current", tree.RescueSurvivors));
        _window.RecoverCorpsesLabel.Text = Loc.GetString("rmc-ui-intel-infinite-progress", ("current", tree.RecoverCorpses));
        _window.ColonyCommunicationsLabel.Text = Loc.GetString("rmc-ui-intel-colony-status", ("online", tree.ColonyCommunications));
        _window.ColonyPowerLabel.Text = Loc.GetString("rmc-ui-intel-colony-status", ("online", tree.ColonyPower));

        _window.CluesContainer.DisposeAllChildren();
        foreach (var (category, clues) in comp.Tree.Clues)
        {
            var scroll = new ScrollContainer
            {
                HScrollEnabled = false,
                VScrollEnabled = true,
            };

            var container = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(4),
            };

            foreach (var (_, clue) in clues)
            {
                container.AddChild(new Label
                {
                    Text = clue,
                    Margin = new Thickness(2, 1, 2, 1),
                    StyleClasses = { "Label" }
                });
            }

            scroll.AddChild(container);
            _window.CluesContainer.AddChild(scroll);
            TabContainer.SetTabTitle(scroll, Loc.GetString(category));
        }
    }
}
