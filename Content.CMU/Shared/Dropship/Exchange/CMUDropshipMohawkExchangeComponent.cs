using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Dropship.Exchange;

/// <summary>
///     Offered on the dynamic gunship while it sits on its round-start pad: the
///     navigation computer can trade the ship for the platoon's standard Mohawk.
///     The gunship refuses to depart for the exchange until everyone steps off,
///     then it flies away and the Mohawk lands on the vacated pad. Any normal
///     takeoff or flyby permanently withdraws the offer.
/// </summary>
[RegisterComponent]
public sealed partial class CMUDropshipMohawkExchangeComponent : Component
{
    /// <summary>Deployment map of the Mohawk that replaces the gunship.</summary>
    [DataField]
    public ResPath MohawkPath;

    /// <summary>Faction the replacement is prepared for, matching the gunship's.</summary>
    [DataField]
    public string Faction = string.Empty;

    /// <summary>Set once the round-start parking flight completes; the offer only exists while parked.</summary>
    [DataField]
    public bool Parked;

    /// <summary>Exchange requested; the gunship departs once the last mob steps off.</summary>
    [DataField]
    public bool Pending;

    /// <summary>The gunship has launched for the exchange and is deleted when it arrives.</summary>
    [DataField]
    public bool Departing;

    /// <summary>Scratch map the departing gunship flies to; deleted with the gunship.</summary>
    [DataField]
    public EntityUid? DepartureMap;
}

[Serializable, NetSerializable]
public sealed class CMUDropshipMohawkExchangeMsg : BoundUserInterfaceMessage;
