using Robust.Shared.Maths;

namespace Content.Server.CMU14.Fob;

/// <summary>Opt-in surface configuration. A malformed configuration leaves legacy defenses untouched.</summary>
[RegisterComponent, Access(typeof(CMUFobMapSystem))]
public sealed partial class CMUFobMapComponent : Component
{
    [DataField(required: true)] public List<EntityUid> Sites = new();
    [DataField] public TimeSpan IntermediateAfter = TimeSpan.FromMinutes(60);
    [DataField] public TimeSpan FarAfter = TimeSpan.FromMinutes(90);
    [ViewVariables] public bool Initialized;
    [ViewVariables] public bool Prepared;
    [ViewVariables] public bool Rejected;
}

/// <summary>Authored landing destination; the first friendly arrival selects one site for the round.</summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(CMUFobMapSystem))]
public sealed partial class CMUFobSiteComponent : Component
{
    [DataField(required: true)] public string Id = string.Empty;
    [DataField] public string Faction = "govfor";
    [DataField(required: true)] public List<CMUFobRelayRoute> Relays = new();
    /// <summary>Surface grid rectangles covering supported dropship footprints, including fixture clearance.</summary>
    [DataField] public List<Box2> UtilityExclusions = new();
    [ViewVariables] public EntityUid? Network;
    [ViewVariables, AutoPausedField] public TimeSpan NextOfferAttempt;
    [ViewVariables] public bool NoRelayPair;
}

/// <summary>Inert editor marker. Only an enabled, selected site creates physical equipment here.</summary>
[RegisterComponent, Access(typeof(CMUFobMapSystem))]
public sealed partial class CMUFobMapAnchorComponent : Component
{
    [DataField] public string? RelayName;
    [ViewVariables] public EntityUid? Relay;
}

[DataDefinition]
public sealed partial class CMUFobRelayRoute
{
    [DataField(required: true)] public EntityUid Anchor;
    [DataField(required: true)] public CMUFobRelayTier Tier;
    [DataField(required: true)] public string Approach = string.Empty;
}

public enum CMUFobRelayTier : byte { Near, Intermediate, Far }

/// <summary>Identifies inherited automatic LZ protection, never ordinary engineering sentries.</summary>
[RegisterComponent]
public sealed partial class CMUFobLegacyDefenseComponent : Component;
