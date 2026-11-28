namespace Content.Shared.CMU14.Threats;

/// <summary>How a threat's player bodies enter the round.</summary>
public enum ThreatEntryMethod : byte
{
    /// <summary>Bodies spawn at the planet's threat spawn markers.</summary>
    Markers,

    /// <summary>Bodies spawn seated aboard a loaded ship, which then flies to a landing zone.</summary>
    Shuttle,
}

/// <summary>Which landing zones a threat ship may pick, in order of preference.</summary>
public enum ThreatLandingZonePolicy : byte
{
    /// <summary>Third-party zones only.</summary>
    ThirdParty,

    /// <summary>Neutral zones (no faction controller) only.</summary>
    Neutral,

    /// <summary>Third-party zones first, then neutral zones.</summary>
    Any,
}
