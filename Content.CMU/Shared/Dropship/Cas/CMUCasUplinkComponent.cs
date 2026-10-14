using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Dropship.Cas;

/// <summary>Gives the wearer of an RTO set the CAS uplink: a terminal for filing close air support requests.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCasUplinkComponent : Component
{
    [DataField]
    public SlotFlags Slot = SlotFlags.BACK;

    [DataField]
    public EntProtoId ActionId = "CMUActionCasUplink";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    /// <summary>Spawned on the nearest hostile when the RTO calls CAS without a flare.</summary>
    [DataField]
    public EntProtoId RadioMark = "CMUCasRadioMark";
}

public sealed partial class CMUCasUplinkActionEvent : InstantActionEvent;

/// <summary>Marks a designator laser spot lased by an RTO; CAS strikes on it are tighter.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCasRtoDesignationComponent : Component;
