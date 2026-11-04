using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Radio;

/// <summary>
///     Makes an ANPRC set a phone on the RMC telephone exchange, sharing the one handset it already has for
///     the net. A call is a radio link: both ends need to be up, powered and unjammed, and either within
///     direct range of each other or both inside relay coverage for the side on their fill card.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ANPRCPhoneComponent : Component
{
    /// <summary>Per fill-card faction, the net whose relay coverage decides whether the set can reach the exchange.</summary>
    [DataField]
    public Dictionary<string, ProtoId<RadioChannelPrototype>> CoverageChannels = new();

    /// <summary>How far apart two sets can be and still call each other with no relay in between, before TX power.</summary>
    [DataField]
    public float DirectRange = 45f;

    [DataField]
    public SlotFlags Slot = SlotFlags.BACK;

    [DataField]
    public EntProtoId ActionId = "AU14ActionANPRCPhone";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;
}

/// <summary>The phone button: answers a ringing set, hangs up a live call, otherwise opens the phone book.</summary>
public sealed partial class ANPRCPhoneActionEvent : InstantActionEvent;
