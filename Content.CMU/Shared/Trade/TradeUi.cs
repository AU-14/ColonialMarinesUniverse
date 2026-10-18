// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Trade;

// Key of the trade platform window.
[Serializable, NetSerializable]
public enum TradeUiKey : byte
{
    Key,
}

// A contract in the platform: the company, its level, and the points of trade (and the points the next level asks for, or null at the last).
[Serializable, NetSerializable]
public sealed class TradeContractEntry(NetEntity contract, string company, int level, int points, int? nextPoints)
{
    public readonly NetEntity Contract = contract;
    public readonly string Company = company;
    public readonly int Level = level;
    public readonly int Points = points;
    public readonly int? NextPoints = nextPoints;
}

// A thing the platform sells. It is locked while the contract it is of has not come to its level. Company is empty for the goods that need no contract.
[Serializable, NetSerializable]
public sealed class TradeOfferEntry(string id, EntProtoId product, string name, int count, int price, string company, int level, bool locked)
{
    public readonly string Id = id;
    public readonly EntProtoId Product = product;
    public readonly string Name = name;
    public readonly int Count = count;
    public readonly int Price = price;
    public readonly string Company = company;
    public readonly int Level = level;
    public readonly bool Locked = locked;
}

// What the trade platform window shows: parcels, their value, tax, payout, contracts and offers.
[Serializable, NetSerializable]
public sealed class TradePlatformBuiState(
    int parcels,
    int parcelValue,
    int taxPercent,
    int payout,
    bool openSky,
    bool busy,
    int maxContracts,
    List<TradeContractEntry> contracts,
    List<TradeOfferEntry> offers) : BoundUserInterfaceState
{
    // The parcels on the pad, what they are worth together by the contracts, the tax of the colony, and what is paid to the seller after it.
    public readonly int Parcels = parcels;
    public readonly int ParcelValue = parcelValue;
    public readonly int TaxPercent = taxPercent;
    public readonly int Payout = payout;

    // The pad stands under the open sky, and the drone is already called.
    public readonly bool OpenSky = openSky;
    public readonly bool Busy = busy;

    public readonly int MaxContracts = maxContracts;
    public readonly List<TradeContractEntry> Contracts = contracts;
    public readonly List<TradeOfferEntry> Offers = offers;
}

// From the window: sell the parcels that are on the platform.
[Serializable, NetSerializable]
public sealed class TradeSellMessage : BoundUserInterfaceMessage;

// From the window: buy this offer.
[Serializable, NetSerializable]
public sealed class TradeBuyMessage(string offer) : BoundUserInterfaceMessage
{
    public readonly string Offer = offer;
}

// From the window: take this contract out of the platform.
[Serializable, NetSerializable]
public sealed class TradeEjectContractMessage(NetEntity contract) : BoundUserInterfaceMessage
{
    public readonly NetEntity Contract = contract;
}

// To one person at the window: the money on their own card, or null when they have no card with an account.
[Serializable, NetSerializable]
public sealed class TradeBalanceMessage(int? balance) : BoundUserInterfaceMessage
{
    public readonly int? Balance = balance;
}
