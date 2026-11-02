// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The two last tabs of the window of the mayor: the growth of the city and the laboratory (where the great sums go), and the projects with the outside and the grey business.
public sealed partial class ColonyGovernmentWindow
{
    // The faction or the city that is picked as the target of a project or of a grey affair.
    private string? _targetId;
    private bool _targetIsFaction;

    // ---------------------------------------------------------------- the growth and the laboratory

    // ---------------------------------------------------------------- the projects and the grey business

    private string ProjectsSignature(ColonyGovernmentState state)
    {
        var growth = state.Growth;
        return "j|" + string.Join(";", growth.Active.Select(a => a.Id))
               + "|" + string.Join(";", growth.Covert.Select(c => $"{c.Id}:{c.Chance}:{c.Blocked.Length > 0}"))
               + "|" + growth.Heat + "|" + growth.Log.Count + "|" + _targetId + ":" + _targetIsFaction;
    }

    private void BuildProjects(ColonyGovernmentState state)
    {
        var growth = state.Growth;

        // Whom the project or the affair is about.
        var targets = Column(6);
        targets.AddChild(Gold(Loc.GetString("rumc-project-title")));
        targets.AddChild(Wrapped(Loc.GetString("rumc-project-hint"), TermPalette.Muted));
        targets.AddChild(Text(Loc.GetString("rumc-project-factions"), TermPalette.Text));
        if (state.Factions.Count == 0)
            targets.AddChild(Muted(Loc.GetString("rumc-government-no-factions")));

        foreach (var faction in state.Factions)
        {
            var name = faction.Name;
            var button = new TermButton { Text = Loc.GetString("rumc-project-target-faction", ("name", faction.Name), ("value", faction.Attitude)), MinWidth = 260, Pressed = _targetIsFaction && _targetId == name };
            button.OnPressed += _ =>
            {
                _targetId = name;
                _targetIsFaction = true;
                Rebuild();
            };
            targets.AddChild(button);
        }

        targets.AddChild(Text(Loc.GetString("rumc-project-cities"), TermPalette.Text));
        foreach (var city in state.Cities.Cities)
        {
            var id = city.Id;
            var button = new TermButton { Text = Loc.GetString("rumc-project-target-city", ("name", city.Name), ("value", city.Relation)), MinWidth = 260, Pressed = !_targetIsFaction && _targetId == id };
            button.OnPressed += _ =>
            {
                _targetId = id;
                _targetIsFaction = false;
                Rebuild();
            };
            targets.AddChild(button);
        }

        _page.AddChild(Card(targets));

        // What stands.
        var active = Column(6);
        active.AddChild(Gold(Loc.GetString("rumc-project-active", ("used", growth.Active.Count), ("max", growth.MaxProjects))));
        if (growth.Active.Count == 0)
            active.AddChild(Muted(Loc.GetString("rumc-project-active-none")));

        foreach (var project in growth.Active)
        {
            var id = project.Id;
            active.AddChild(Rule());
            active.AddChild(Text(Loc.GetString("rumc-project-active-line", ("name", project.Name), ("target", project.Target)), TermPalette.Text));
            if (project.Permanent)
                active.AddChild(Muted(Loc.GetString("rumc-project-permanent")));
            else
                active.AddChild(Live(s => Loc.GetString("rumc-project-left", ("time", Time(s.Growth.Active.FirstOrDefault(a => a.Id == id)?.LeftSeconds ?? 0))), TermPalette.Muted));

            active.AddChild(Danger(Loc.GetString("rumc-project-close"), () => OnCancelProject?.Invoke(id), true, 190));
        }

        _page.AddChild(Card(active));

        // What can be started.
        foreach (var project in growth.Projects)
        {
            var id = project.Id;
            var column = Column(4);
            column.AddChild(Text(project.Name, TermPalette.Text));
            column.AddChild(Wrapped(project.Description, TermPalette.Muted));
            column.AddChild(Muted(Loc.GetString("rumc-project-cost", ("cost", Money(project.Cost)), ("upkeep", Money(project.Upkeep)),
                ("time", project.Duration == 0 ? Loc.GetString("rumc-project-permanent") : Time(project.Duration)))));
            if (project.Effects.Length > 0)
                column.AddChild(Text(Loc.GetString("rumc-government-effects") + " " + project.Effects, TermPalette.Text));

            if (project.Requirement.Length > 0)
                column.AddChild(Muted(Loc.GetString("rumc-government-accord-requires", ("kind", project.Requirement))));

            var wantsFaction = project.Kind == ProjectKind.Embassy;
            var fits = _targetId != null && _targetIsFaction == wantsFaction;
            column.AddChild(Muted(Loc.GetString(wantsFaction ? "rumc-project-needs-faction" : "rumc-project-needs-city")));
            column.AddChild(Action(Loc.GetString("rumc-project-start"), () =>
            {
                if (_targetId is { } target)
                    OnStartProject?.Invoke(id, target);
            }, fits && state.Budget >= project.Cost, 190));
            _page.AddChild(Card(column));
        }

        // The grey business.
        var grey = Column(6);
        grey.AddChild(Gold(Loc.GetString("rumc-covert-title")));
        grey.AddChild(Wrapped(Loc.GetString("rumc-covert-hint"), TermPalette.Muted));
        grey.AddChild(Text(Loc.GetString("rumc-covert-heat", ("value", growth.Heat)), growth.Heat >= 50 ? TermPalette.Bad : TermPalette.Gold));
        _page.AddChild(Card(grey));

        foreach (var affair in growth.Covert)
        {
            var id = affair.Id;
            var column = Column(4);
            column.AddChild(Text(affair.Name, TermPalette.Text));
            column.AddChild(Wrapped(affair.Description, TermPalette.Muted));
            column.AddChild(Muted(Loc.GetString("rumc-covert-cost", ("cost", Money(affair.Cost)), ("chance", affair.Chance))));
            column.AddChild(Muted(affair.Effects));
            if (affair.Blocked.Length > 0)
                column.AddChild(Text(affair.Blocked, TermPalette.Bad));

            var fits = affair.Target switch
            {
                CovertTarget.None => true,
                CovertTarget.Faction => _targetId != null && _targetIsFaction,
                _ => _targetId != null && !_targetIsFaction,
            };

            if (affair.Target != CovertTarget.None)
                column.AddChild(Muted(Loc.GetString(affair.Target == CovertTarget.Faction ? "rumc-project-needs-faction" : "rumc-project-needs-city")));

            column.AddChild(Action(Loc.GetString("rumc-covert-run"), () => OnRunCovert?.Invoke(id, _targetId ?? string.Empty), fits && affair.Blocked.Length == 0 && state.Budget >= affair.Cost, 190));
            _page.AddChild(Card(column));
        }

        var log = Column(4);
        log.AddChild(Gold(Loc.GetString("rumc-covert-log")));
        if (growth.Log.Count == 0)
            log.AddChild(Muted(Loc.GetString("rumc-covert-log-none")));

        foreach (var line in growth.Log)
            log.AddChild(Wrapped(line, TermPalette.Muted));

        _page.AddChild(Card(log));
    }

    // ---------------------------------------------------------------- the quota of the metropolis

    private static string QuotasSignature(ColonyGovernmentState state)
    {
        var quota = state.Quota;
        return "q|" + quota.Active + ":" + quota.Amount + ":" + quota.Log.Count;
    }

    private void BuildQuotas(ColonyGovernmentState state)
    {
        var quota = state.Quota;

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-quota-title")));
        if (!quota.Exists)
        {
            head.AddChild(Muted(Loc.GetString("rumc-quota-none")));
            _page.AddChild(Card(head));
            return;
        }

        head.AddChild(Text(quota.Name, TermPalette.Text));
        head.AddChild(Wrapped(quota.Description, TermPalette.Muted));
        head.AddChild(Muted(Loc.GetString("rumc-quota-terms", ("amount", Money(quota.Amount)), ("grant", Money(quota.Grant)), ("fine", Money(quota.Fine)), ("level", quota.Level))));
        _page.AddChild(Card(head));

        var plan = Column(6);
        if (quota.Active)
        {
            plan.AddChild(Gold(Loc.GetString("rumc-quota-active")));
            plan.AddChild(Live(s => Loc.GetString("rumc-quota-progress", ("paid", Money(s.Quota.Paid)), ("amount", Money(s.Quota.Amount)),
                ("percent", s.Quota.Amount == 0 ? 0 : s.Quota.Paid * 100 / s.Quota.Amount)), TermPalette.Gold));
            plan.AddChild(Live(s => Loc.GetString("rumc-quota-left", ("time", Time(s.Quota.LeftSeconds))), TermPalette.Text));

            var left = quota.Amount - quota.Paid;
            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-quota-pay", ("sum", Money(100))), () => OnPayQuota?.Invoke(100), state.Budget >= 1 && left > 0, 170));
            buttons.AddChild(Action(Loc.GetString("rumc-quota-pay", ("sum", Money(500))), () => OnPayQuota?.Invoke(500), state.Budget >= 1 && left > 0, 170));
            buttons.AddChild(Action(Loc.GetString("rumc-quota-pay-rest", ("sum", Money(left))), () => OnPayQuota?.Invoke(left), state.Budget >= 1 && left > 0, 220));
            plan.AddChild(buttons);
            plan.AddChild(Wrapped(Loc.GetString("rumc-quota-active-hint"), TermPalette.Muted));
        }
        else
        {
            plan.AddChild(Gold(Loc.GetString("rumc-quota-waiting")));
            plan.AddChild(Live(s => Loc.GetString("rumc-quota-next", ("time", Time(s.Quota.NextSeconds))), TermPalette.Text));
        }

        _page.AddChild(Card(plan));

        var log = Column(4);
        log.AddChild(Gold(Loc.GetString("rumc-quota-log")));
        if (quota.Log.Count == 0)
            log.AddChild(Muted(Loc.GetString("rumc-quota-log-none")));

        foreach (var line in quota.Log)
            log.AddChild(Wrapped(line, TermPalette.Muted));

        _page.AddChild(Card(log));
    }
}
