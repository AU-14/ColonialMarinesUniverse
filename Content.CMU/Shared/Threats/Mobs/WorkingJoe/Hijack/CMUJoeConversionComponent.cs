using Content.Shared.Antag;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;

/// <summary>
/// Enables Offer's conversion of a dead synthetic into a hijacked Working Joe. Carried by hijacked Joes and by every
/// Early Warning Joe in a round with a Working Joes shipment. The conversion data lives here so the kit stays in
/// prototypes. Not networked.
/// </summary>
[RegisterComponent]
public sealed partial class CMUJoeConversionComponent : Component
{
    /// <summary>How long the Offer on a dead synthetic takes.</summary>
    [DataField]
    public TimeSpan ConversionDelay = TimeSpan.FromSeconds(5);

    /// <summary>The components, mind roles and briefing a conversion applies, read as data.</summary>
    [DataField]
    public ProtoId<AntagSpecifierPrototype> Specifier = "CMUHijackedJoeConversion";

    /// <summary>The actions a converted Joe is granted.</summary>
    [DataField]
    public List<EntProtoId> Actions = new() { "CMUActionDrown", "CMUActionOpticalZoom", "CMUActionOffer" };
}
