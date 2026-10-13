using Content.Client.CMU14.Overwatch;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared.CMU14.Overwatch;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Overwatch;

// CMU14: CRT look, grouped roster with callsigns and casualty info, supply drop readiness and
// up to four camera feeds.
public sealed partial class OverwatchConsoleBui
{
    private readonly Dictionary<NetEntity, CMUOverwatchRoster> _cmuRosters = new();

    private void CMUOnWindowCreated()
    {
        if (Window == null || !StyleNano.CrtUiEnabled)
            return;

        CrtLobbyTheme.ApplyWindow(Window, useCrtTypography: true);
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
            roster.OnWatch += id => SendPredictedMessage(new OverwatchConsoleWatchBuiMsg(id));
            roster.OnFeed += id => SendMessage(new CMUOverwatchFeedToggleBuiMsg(id));
            roster.OnHide += (id, hide) => SendPredictedMessage(new OverwatchConsoleHideBuiMsg(id, hide));
            roster.OnPromote += id => SendPredictedMessage(new OverwatchConsolePromoteLeaderBuiMsg(id, squad.LeaderIcon));
            tableParent.AddChild(roster);

            _cmuRosters[squad.Id] = roster;
            monitor.SearchBar.OnTextChanged += _ => Refresh();
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
        if (!_cmuRosters.TryGetValue(squad.Id, out var roster))
            return;

        state.Fireteams.TryGetValue(squad.Id, out var fireteams);
        roster.Update(
            entries,
            squad.Leader,
            fireteams,
            console,
            id => _overwatchConsole.IsHidden((Owner, console), id),
            monitor.SearchBar.Text,
            EntMan.System<CMUOverwatchFeedsSystem>().GetFeedTargets());
    }

    private void CMUApplyTheme()
    {
        if (Window != null)
            CMUOverwatchCrt.Apply(Window);
    }
}
