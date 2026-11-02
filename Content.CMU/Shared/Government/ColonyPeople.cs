// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// What the mayor and the people of the city do together. The townsfolk are real players, so nothing here counts them as a crowd: the mayor rewards a player, orders the work that a player does and pays
// for it, answers the appeals that the players write, asks them to vote, and the notary of the city puts a seal on the deals between them. The tables are here; what is done with them is in
// Server/Government.

[Serializable, NetSerializable]
public enum AwardCategory : byte
{
    Guard,
    Build,
    Trade,
    Other,
}

// A reward that the mayor can give to a player who stands by the console: a charter (a paper with the words and the seal), a medal, and money out of the budget. The cost is paid out of the budget, and
// the cash goes to the bank account of the one who is rewarded.
[Prototype("colonyAward")]
public sealed partial class ColonyAwardPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public AwardCategory Category;

    // What it costs the budget to make (the charter, the medal), not counting the cash.
    [DataField]
    public int Cost = 100;

    // The money that the player gets on his account.
    [DataField]
    public int Cash;

    // The medal that is put into the hands of the player, if any.
    [DataField]
    public EntProtoId? Medal;

    // Whether a charter is printed.
    [DataField]
    public bool Charter = true;
}

// A thing that the mayor can order from the players: the player brings it to the console and gets the price for each. The thing is known by the entity or by the stack that it is made of.
[Prototype("colonyOrderKind")]
public sealed partial class ColonyOrderKindPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public EntProtoId? Item;

    [DataField]
    public ProtoId<StackPrototype>? Stack;

    // The price that is thought fair for a piece: the mayor may pay from half of it to thrice as much.
    [DataField]
    public int UnitPrice = 20;

    [DataField]
    public int MaxAmount = 100;
}

[Serializable, NetSerializable]
public enum ContractMode : byte
{
    Lease,
    PlotSale,
    Pledge,
}

// A kind of deal that the notary of the city seals. The fee is shared by the two who make it and goes to the budget.
[Prototype("colonyContractKind")]
public sealed partial class ColonyContractKindPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public ContractMode Mode;

    [DataField]
    public int Fee = 100;
}

// A mark on the console of the people (the one where the townsfolk write to the mayor, vote, take orders and make deals): its window is served by Server/Government.
[RegisterComponent]
public sealed partial class CitizenConsoleComponent : Component;
