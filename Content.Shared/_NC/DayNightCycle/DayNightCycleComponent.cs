using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._NC14.DayNightCycle
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class DayNightCycleComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("cycleDuration")]
        public float CycleDurationMinutes { get; set; } = 45f; // Default cycle duration is 45 minutes

        [DataField("timeEntries")]
        public List<TimeEntry> TimeEntries { get; set; } = new()
        {
            new() { Time = 0.04f, ColorHex = "#02020B" }, // Very early morning
            new() { Time = 0.08f, ColorHex = "#241A13" }, // Early dawn
            new() { Time = 0.17f, ColorHex = "#3B2A1E" }, // Dawn
            new() { Time = 0.25f, ColorHex = "#7A5436" }, // Sunrise
            new() { Time = 0.33f, ColorHex = "#DCA865" }, // Early morning
            new() { Time = 0.42f, ColorHex = "#FFF4E3" }, // Mid-morning
            new() { Time = 0.50f, ColorHex = "#FFFFFF" }, // Noon
            new() { Time = 0.58f, ColorHex = "#F7F7F7" }, // Early afternoon
            new() { Time = 0.67f, ColorHex = "#F6C07E" }, // Late afternoon
            new() { Time = 0.75f, ColorHex = "#DC8F42" }, // Sunset
            new() { Time = 0.83f, ColorHex = "#5A3A69" }, // Dusk
            new() { Time = 0.92f, ColorHex = "#02020B" }, // Early night
            new() { Time = 1.00f, ColorHex = "#000000" }   // Midnight
        };

        [ViewVariables(VVAccess.ReadWrite)]
        [AutoNetworkedField]
        public int CurrentTimeEntryIndex { get; set; }

        [ViewVariables(VVAccess.ReadWrite)]
        [AutoNetworkedField]
        public float CurrentCycleTime { get; set; }
    }

    [DataDefinition, NetSerializable, Serializable]
    public sealed partial class TimeEntry
    {
        [DataField("colorHex")]
        public string ColorHex { get; set; } = "#FFFFFF";

        [DataField("time")]
        public float Time { get; set; } // Normalized time (0-1)
    }
}