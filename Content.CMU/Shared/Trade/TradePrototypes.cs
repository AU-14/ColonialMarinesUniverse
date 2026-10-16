// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Trade;

// A contract of a company. A contract put into a trade platform opens the assortment of the company: what its drones buy (and for how much), and what they sell. Every trade through
// the contract raises its standing with the company, up to the third level, and the higher level opens the rarer goods for buying.
[Prototype("tradeContract")]
public sealed partial class TradeContractPrototype : IPrototype
{
    public const int MaxLevel = 3;

    [IdDataField]
    public string ID { get; private set; } = default!;

    // The name of the company, and the category of the goods that the contract is of: the company has a contract for each.
    [DataField(required: true)]
    public LocId Company;

    [DataField(required: true)]
    public LocId Category;

    [DataField]
    public LocId? Description;

    // The colour of the paper of the contract.
    [DataField]
    public Color Color = Color.White;

    // The points of trade it takes to come to the second and to the third level. A point is a coin of what was sold to the company or bought of it.
    [DataField]
    public List<int> LevelPoints = new() { 200, 1000 };

    [DataField(required: true)]
    public List<TradeGood> Goods = new();

    public int LevelOf(int points)
    {
        var level = 1;
        foreach (var needed in LevelPoints)
        {
            if (level < MaxLevel && points >= needed)
                level++;
        }

        return level;
    }

    // The points the next level asks for, or null at the last one.
    public int? NextLevelPoints(int points)
    {
        var level = LevelOf(points);
        return level >= MaxLevel || level - 1 >= LevelPoints.Count ? null : LevelPoints[level - 1];
    }
}

// One line of the assortment of a contract: a thing (or a kind of things) that the drones buy for Sell each, and that they sell for Buy for a lot of Count.
[DataDefinition]
public sealed partial class TradeGood
{
    // The thing is a stack of this type...
    [DataField]
    public string? Stack;

    // ...or an entity of one of these prototypes, or made from one of them...
    [DataField]
    public List<string> Entities = new();

    // ...or an entity that has one of these components.
    [DataField]
    public List<string> Components = new();

    // What the drone pays for one of it, or 0 when it is not bought.
    [DataField]
    public float Sell;

    // What a lot of it costs when it is bought, or 0 when it is not sold.
    [DataField]
    public int Buy;

    // What is bought: for a stack it is the entity of the stack, and it can be left out; for anything else it is needed.
    [DataField]
    public EntProtoId? Product;

    // How many come in the lot (for a stack it is the count of the stack).
    [DataField]
    public int Count = 1;

    // The level of the contract that opens the thing for buying. Selling is open from the first.
    [DataField]
    public int Level = 1;
}
