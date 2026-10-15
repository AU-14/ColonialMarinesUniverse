using Content.Client.CMU14.Overwatch;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared.CMU14.Overwatch;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Overwatch;

// CMU14: CRT look, grouped roster with callsigns and casualty info, supply drop readiness and
// up to four camera feeds.
public sealed partial class OverwatchConsoleBui
{
    private readonly Dictionary<NetEntity, CMUOverwatchRoster> _cmuRosters = new();
    private readonly Dictionary<NetEntity, CMUOverwatchFireteamPanel> _cmuFireteamPanels = new();

    private bool _cmuRestoredView;

    private void CMUOnWindowCreated()
    {
        if (Window == null)
            return;

        CMURestoreViewSettings();

        // The console stays in the game window; popping it out to its own OS window is not offered.
        CMUHidePopOutButton(Window);

        if (StyleNano.CrtUiEnabled)
            CrtLobbyTheme.ApplyWindow(Window, useCrtTypography: true);
    }

    private static bool CMUHidePopOutButton(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is Button { Text: "Pop Out" } button)
            {
                button.Visible = false;
                return true;
            }

            if (CMUHidePopOutButton(child))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Squad picker buttons keep the squad's colour on their text rather than tinting the whole button.
    /// </summary>
    private static void CMUStyleSquadButton(Button button, Color color)
    {
        if (!StyleNano.CrtUiEnabled)
            return;

        button.ModulateSelfOverride = null;
        button.Label.FontColorOverride = color;
    }

    private void CMUSetupSquadView(OverwatchSquad squad, OverwatchSquadView monitor)
    {
        // The roster replaces upstream's column table, which stays in the tree (hidden) so the
        // upstream code that fills it keeps working unchanged.
        if (monitor.Names.Parent?.Parent?.Parent is ScrollContainer table && table.Parent is { } tableParent)
        {
            table.Visible = false;

            var roster = new CMUOverwatchRoster();
            roster.OnFeed += id => SendMessage(new CMUOverwatchFeedToggleBuiMsg(id));
            roster.OnHide += (id, hide) => SendPredictedMessage(new OverwatchConsoleHideBuiMsg(id, hide));
            roster.OnPromote += id => SendPredictedMessage(new OverwatchConsolePromoteLeaderBuiMsg(id, squad.LeaderIcon));
            tableParent.AddChild(roster);

            _cmuRosters[squad.Id] = roster;
            monitor.SearchBar.OnTextChanged += _ => Refresh();
        }

        if (monitor.Longitudes.Parent?.Parent is { } supplySaved)
            supplySaved.Visible = true;

        var fireteams = new CMUOverwatchFireteamPanel();
        fireteams.OnAssign += (id, fireteam) => SendMessage(new CMUOverwatchAssignFireteamBuiMsg(id, fireteam));
        fireteams.OnLead += (id, lead) => SendMessage(new CMUOverwatchFireteamLeaderBuiMsg(id, lead));
        fireteams.OnAutoLeaders += enabled => SendMessage(new CMUOverwatchAutoLeadersBuiMsg(enabled));
        fireteams.OnNickname += (index, nickname) =>
            SendMessage(new OverwatchConsoleSetFireteamNicknameBuiMsg(squad.Id, index, nickname));
        monitor.FireteamsList.AddChild(fireteams);
        _cmuFireteamPanels[squad.Id] = fireteams;

        // Upstream's "Fireteams" title bar is set to expand and takes half the tab; the panel has
        // its own headings, so drop the bar and let the list fill the tab.
        if (monitor.FireteamsList.Parent is ScrollContainer fireteamScroll)
        {
            fireteamScroll.HScrollEnabled = false;
            fireteamScroll.VerticalExpand = true;

            if (fireteamScroll.Parent is { } fireteamTab)
            {
                foreach (var child in fireteamTab.Children)
                {
                    if (child != fireteamScroll)
                        child.Visible = false;
                }
            }
        }

        if (EntMan.HasComponent<SupplyDropComputerComponent>(Owner) &&
            squad.CanSupplyDrop &&
            monitor.TacticalMapButton.Parent is { } header)
        {
            var readiness = new CMUSupplyReadinessLabel(Owner);
            header.AddChild(readiness);
            readiness.SetPositionInParent(monitor.TacticalMapButton.GetPositionInParent());
        }
    }

    private void CMUUpdateRoster(
        OverwatchSquad squad,
        OverwatchSquadView monitor,
        List<CMURosterEntry> entries,
        OverwatchConsoleBuiState state,
        OverwatchConsoleComponent console)
    {
        state.Fireteams.TryGetValue(squad.Id, out var fireteams);

        if (_cmuFireteamPanels.TryGetValue(squad.Id, out var fireteamPanel))
            fireteamPanel.Update(fireteams, !squad.ManualLeadership);

        if (!_cmuRosters.TryGetValue(squad.Id, out var roster))
            return;
        roster.Update(
            entries,
            squad.Leader,
            fireteams,
            console,
            id => _overwatchConsole.IsHidden((Owner, console), id),
            monitor.SearchBar.Text,
            EntMan.System<CMUOverwatchFeedsSystem>().GetFeedTargets());
    }

    /// <summary>
    /// Puts the operator's own filter choices back when they reopen a console, since the console
    /// itself may have been changed by someone else meanwhile.
    /// </summary>
    private void CMURestoreViewSettings()
    {
        if (!EntMan.TryGetComponent(Owner, out OverwatchConsoleComponent? console))
            return;

        var saved = EntMan.System<CMUOverwatchViewSettingsSystem>().Settings;
        if (saved != null)
        {
            if (console.ShowDead != saved.Value.ShowDead)
                SendPredictedMessage(new OverwatchConsoleShowDeadBuiMsg(saved.Value.ShowDead));

            if (console.ShowHidden != saved.Value.ShowHidden)
                SendPredictedMessage(new OverwatchConsoleShowHiddenBuiMsg(saved.Value.ShowHidden));

            if (console.Location != saved.Value.Location)
                SendPredictedMessage(new OverwatchConsoleSetLocationBuiMsg(saved.Value.Location));
        }

        _cmuRestoredView = true;
    }

    private void CMURememberViewSettings()
    {
        if (!_cmuRestoredView || !EntMan.TryGetComponent(Owner, out OverwatchConsoleComponent? console))
            return;

        var settings = EntMan.System<CMUOverwatchViewSettingsSystem>();
        var squadInfoHidden = settings.Settings?.SquadInfoHidden ?? false;
        if (console.Squad is { } active && _squadViews.TryGetValue(active, out var view))
            squadInfoHidden = view.CMUSquadInfoHidden;

        settings.Settings = new CMUOverwatchViewSettings(console.Location, console.ShowDead, console.ShowHidden, squadInfoHidden);

        foreach (var squadView in _squadViews.Values)
        {
            squadView.CMUSetSquadInfoHidden(squadInfoHidden);
        }
    }

    private void CMUApplyTheme()
    {
        CMURememberViewSettings();

        if (Window != null)
            CMUOverwatchCrt.Apply(Window);
    }
}
