using Content.Shared.Whitelist;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// "Offer N items": counts whitelisted items the Joe consumed with the Offer action while this objective
/// was unfinished. An offered item is deleted, so the count is kept here rather than as a set of
/// entities, and an item can never count twice.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeOfferConditionComponent : Component
{
    /// <summary>
    /// What may be offered. Components and tags are both honoured. COUPLING-02: this decides the kind of item
    /// (firearms, grenades); <see cref="DefaultFilter"/> keeps machinery and bodies out whatever it names.
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    /// <summary>Whitelisted items that still may not be offered.</summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// Apply the default offering filter (<c>EWJoeOfferSystem.IsOfferable</c>): a carried item, not anchored,
    /// not a sentry, a mob, an attachment or a vehicle hardpoint.
    /// </summary>
    [DataField]
    public bool DefaultFilter = true;

    /// <summary>
    /// ARCH-11, a Meld directive: what it wants is a structure (a camera, a telecomms server), so the default
    /// filter does not apply and the offering is not deleted. It is melded into the river instead
    /// (<c>EWJoeMeldedStructureSystem.Meld</c>): marked, lit orange and switched off. A melded structure is
    /// never offered again, which is what keeps it from counting twice.
    /// </summary>
    [DataField]
    public bool Meld;

    /// <summary>
    /// Each offering counts its <c>CMUThreatStructureComponent.Weight</c> (cores 3) rather than 1. Things of
    /// weight zero (spreading growth) are not wanted at all.
    /// </summary>
    [DataField]
    public bool Weighted;

    /// <summary>Inclusive bounds the target count is rolled from.</summary>
    [DataField]
    public int Min = 1;

    [DataField]
    public int Max = 1;

    /// <summary>The rolled target. Set on assignment.</summary>
    [DataField]
    public int Target;

    /// <summary>Offerings counted toward this objective so far (by weight where <see cref="Weighted"/>).</summary>
    [DataField]
    public int Offered;

    /// <summary>
    /// The map of the planet the directive was given on (set on assignment). Only things on that planet's z-level
    /// network count, so a ship's cameras or servers never finish a planet directive.
    /// </summary>
    [DataField]
    public MapId? OfferMap;

    /// <summary>Title locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Description locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Description;
}
