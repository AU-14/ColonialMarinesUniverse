namespace Content.Shared.CMU14.Callsigns;

// raised on the squad menu's owner; the server callsign system fills Callsigns,
// limited to the owner's own faction. kept as an event so the shared squad tracker
// never touches the server-only callsign system and the callsigns never ride on a
// networked component
[ByRefEvent]
public record struct AU14GetSquadCallsignsEvent(IEnumerable<EntityUid> Members, Dictionary<NetEntity, string> Callsigns);

// raised after a callsign is (re)assigned, so open squad menus can refresh
[ByRefEvent]
public readonly record struct AU14CallsignChangedEvent(EntityUid Member);
