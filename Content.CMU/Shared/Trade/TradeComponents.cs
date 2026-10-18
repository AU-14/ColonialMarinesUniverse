// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Trade;

// A sealed parcel: a cardboard box with things packed in it. Sold on a trade platform, it is priced by what is inside. It is not opened by hand: it is torn open.
[RegisterComponent, NetworkedComponent]
public sealed partial class TradeParcelComponent : Component
{
    public const string ContainerId = "parcel";

    [DataField]
    public TimeSpan UnpackTime = TimeSpan.FromSeconds(3);
}

// The packer: it takes cardboard and things, and packs the things into a parcel.
[RegisterComponent, NetworkedComponent]
public sealed partial class TradePackerComponent : Component
{
    public const string ContainerId = "packer_items";

    // The stack the packer asks for.
    [DataField]
    public string CardboardStack = "RMCSheetCardboard";

    // One sheet of cardboard seals this many things, and any part of it counts as a whole.
    [DataField]
    public int ItemsPerSheet = 8;

    [DataField]
    public int MaxItems = 24;

    [DataField]
    public int MaxCardboard = 20;

    [DataField]
    public TimeSpan PackTime = TimeSpan.FromSeconds(2.5);

    // Cardboard loaded into the machine and not used yet.
    [DataField]
    public int Cardboard;

    [DataField]
    public string ParcelPrototype = "RUMCTradeParcel";
}

// The pad: a parcel put on it is sold, a purchase is left on it. Both are done by a drone that comes ten seconds after the call, under the open sky only.
[RegisterComponent, NetworkedComponent]
public sealed partial class TradePlatformComponent : Component
{
    public const string ContractsContainerId = "contracts";

    // How long the drone takes to come after the call.
    [DataField]
    public TimeSpan DroneDelay = TimeSpan.FromSeconds(10);

    // The parcels within this distance of the middle of the pad are on it.
    [DataField]
    public float Radius = 0.6f;

    // How many contracts one platform can hold.
    [DataField]
    public int MaxContracts = 9;

    [DataField]
    public string DronePrototype = "RUMCTradeDrone";

    // The call that is being done: the platform takes one at a time.
    public TradeOrder? Order;

    // The drone that is on its way, or at the pad.
    public EntityUid? Drone;

    // When the open window of the platform was last refreshed.
    public TimeSpan NextRefresh;

    [DataField]
    public string ParcelPrototype = "RUMCTradeParcel";
}

// The stages of a trade drone visit: descending, hovering, ascending.
public enum TradeDronePhase : byte
{
    // The drone comes out of the sky and settles down on the pad.
    Descend,

    // It does what it came for.
    Hover,

    // It rises and is gone.
    Ascend,
}

// The drone that comes to a trade platform: which platform, which stage it is in, and how long each stage lasts.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class TradeDroneComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Platform;

    [DataField, AutoNetworkedField]
    public TradeDronePhase Phase;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan PhaseStart;

    [DataField]
    public TimeSpan DescendTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan HoverTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan AscendTime = TimeSpan.FromSeconds(3);

    // How high above the pad (in tiles) the drone comes out of the sky.
    [DataField]
    public float Height = 7f;

}

// What a drone is called for: to take a sale away or to bring a purchase.
public enum TradeOrderKind : byte
{
    Sell,
    Buy,
}

// A call of a drone, as the server holds it until the drone leaves.
public sealed class TradeOrder
{
    public TradeOrderKind Kind;
    public TimeSpan ArriveAt;

    // The card the money goes to or comes from.
    public EntityUid Card;

    // For a purchase: what was bought, and what was paid for it (paid at the call, given back if the drone does not come).
    public string? Offer;
    public string? Product;
    public int Count = 1;

    // The contract that the purchase is of (it gets the points of it), when it is of one.
    public EntityUid? Contract;
    public int Paid;
}

// The do-after of packing goods into a parcel.
[Serializable, NetSerializable]
public sealed partial class TradePackDoAfterEvent : SimpleDoAfterEvent;

// The do-after of unpacking a parcel.
[Serializable, NetSerializable]
public sealed partial class TradeUnpackDoAfterEvent : SimpleDoAfterEvent;

// The paper of a contract with a company, put into a trade platform. It carries the standing of the colony with the company: the points of trade, from which the level is made.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TradeContractComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TradeContractPrototype> Contract;

    [DataField, AutoNetworkedField]
    public int Points;
}
