using Content.Shared.Whitelist;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// "Assimilate a console": on assignment pins one console from the whitelist on the owner's planet
/// (its z-level network of maps), and
/// completes once that console carries EWJoeAssimilatedConsoleComponent. The console types follow
/// decisions D6 and D12; the picker tolerates planets that lack a type (Garrison has no comms console).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeAssimilateConditionComponent : Component
{
    /// <summary>Console component names that qualify. Only the component list is used; tags are ignored.</summary>
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    /// <summary>The pinned console. Set on assignment.</summary>
    [DataField]
    public EntityUid? Target;

    /// <summary>
    /// Playtest 4: the ladder credited this objective. A repaired console it names is not the Joe's to take
    /// again (the assimilate action is gated on objectives, and each assimilation brings a meld or respawn).
    /// </summary>
    [DataField]
    public bool Spent;

    /// <summary>Title locale id; receives "console" and "area".</summary>
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Description locale id; receives "console" and "area".</summary>
    [DataField(required: true)]
    public LocId Description;
}
