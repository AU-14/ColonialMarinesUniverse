using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Fob;

namespace Content.Client._RMC14.TacticalMap;

public sealed partial class TacticalMapComputerBui
{
    private Dictionary<int, TacticalMapBlip> _fobMarkers = new();

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not CMUFobMapMarkersMessage markers) return;
        _fobMarkers = markers.Markers;
        Refresh();
    }

    private Dictionary<int, TacticalMapBlip> WithFobMarkers(Dictionary<int, TacticalMapBlip> source)
    {
        if (_fobMarkers.Count == 0) return source;
        var combined = new Dictionary<int, TacticalMapBlip>(source);
        foreach (var (key, value) in _fobMarkers) combined[key] = value;
        return combined;
    }
}
