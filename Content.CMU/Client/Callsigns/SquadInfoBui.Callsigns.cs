using Content.Shared._RMC14.Tracker.SquadLeader;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Tracker.SquadLeader;

// CMU14 callsigns next to names in the squad menu
public sealed partial class SquadInfoBui
{
    // CMU14: callsigns from the latest BUI state, set at the top of Refresh()
    private Dictionary<NetEntity, string> _cmuCallsigns = new();

    // CMU14 method: plain text "Name (CALLSIGN)" for the leader labels
    private string CMUWithCallsign(string name, NetEntity? id)
    {
        return id is { } netId && _cmuCallsigns.TryGetValue(netId, out var callsign) && !string.IsNullOrEmpty(callsign)
            ? $"{name} ({callsign})"
            : name;
    }

    // CMU14 method: bold name followed by a dimmed callsign for the member rows
    private string CMUFormatName(SquadLeaderTrackerMarine member)
    {
        var name = $"[bold]{FormattedMessage.EscapeText(member.Name)}[/bold]";
        if (_cmuCallsigns.TryGetValue(member.Id, out var callsign) && !string.IsNullOrEmpty(callsign))
            name += $" [color=#9aa5b1]({FormattedMessage.EscapeText(callsign)})[/color]";

        return name;
    }
}
