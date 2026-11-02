using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.TacticalMap.Scout;
using Robust.Shared.Map;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    [Dependency] private AreaSystem _scoutAreas = default!;
    [Dependency] private RMCPlanetSystem _scoutPlanet = default!;
    [Dependency] private RadioSystem _scoutRadio = default!;

    private void TransmitScoutReport(EntityUid scout, string faction, CMUScoutContactType contactType,
        MapCoordinates observed, CMUScoutReportSettingsPrototype settings)
    {
        if (!settings.RadioChannels.TryGetValue(faction, out var channels) || channels.Length == 0)
        {
            ScoutReportPopup(scout, "cmu-scout-report-no-radio-channel");
            return;
        }

        if (!TryComp(scout, out AU14CallsignComponent? callsign) ||
            string.IsNullOrWhiteSpace(callsign.Callsign) ||
            !TryNormalizeHumanFaction(callsign.Faction, out var callsignFaction) || callsignFaction != faction)
        {
            ScoutReportPopup(scout, "cmu-scout-report-no-callsign");
            return;
        }

        var position = observed.Position.Floored();
        if (_scoutPlanet.TryGetOffset(observed, out var offset))
            position += offset;
        var area = _scoutAreas.TryGetArea(observed, out _, out var areaProto)
            ? areaProto.Name
            : Loc.GetString("cmu-scout-report-unknown-area");
        var message = Loc.GetString("cmu-scout-report-radio",
            ("callsign", callsign.Callsign),
            ("contact", CMUScoutReportInfo.ContactName(contactType)),
            ("x", position.X),
            ("y", position.Y),
            ("area", area));

        // These are ordinary transmissions from the observer, not unjammable automated beacon calls.
        foreach (var channel in channels)
            _scoutRadio.SendRadioMessage(scout, message, channel, scout, null, senderName: callsign.Callsign);
    }
}
