// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Mining;

// A deposit of ore lying on the floor, to be drilled. It is made when it appears: up to five kinds of ore, picked by the weights
// of its table, with an amount of each. The drill takes them out of it a few at a time, and the deposit is gone when it is empty.
[RegisterComponent]
public sealed partial class OreDepositComponent : Component
{
    [DataField(required: true)]
    public ProtoId<OreDepositTablePrototype> Table;

    // How many kinds of ore a deposit can hold at most.
    [DataField]
    public int MaxKinds = 5;

    // The amount of ore in all, drawn between these when the deposit is made.
    [DataField]
    public int MinTotal = 10000;

    [DataField]
    public int MaxTotal = 20000;

    // What is left in the deposit, by stack of ore.
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Ores = new();

    [DataField]
    public bool Generated;
}

// What ores a kind of deposit can hold, and how likely each of them is, as weights.
[Prototype("cmuOreDepositTable")]
public sealed partial class OreDepositTablePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<ProtoId<StackPrototype>, float> Ores = new();
}

// The head of a drill: it wears out with every piece of ore the drill takes and breaks when it is gone.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DrillHeadComponent : Component
{
    // How many turns of drilling the head lasts, new.
    [DataField]
    public int MaxDurability = 100;

    [DataField, AutoNetworkedField]
    public int Durability = 100;
}

// A drill: set on a deposit of ore, with a head in its head slot, it takes ore out of the deposit and puts it in its output
// slots. The steam drill runs on steam like the other steam machines, the electric one on power like the electric smelter.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OreDrillComponent : Component
{
    public const string HeadSlotId = "head";
    public static readonly string[] OutputSlotIds = { "output1", "output2", "output3", "output4", "output5", "output6" };

    // Seconds of drilling for one turn, and the most ore one turn gives.
    [DataField]
    public float CycleSeconds = 8f;

    [DataField]
    public int MinPerCycle = 1;

    [DataField]
    public int MaxPerCycle = 10;

    // Moles of water vapor used every second of work; zero for the drill that runs on the grid.
    [DataField]
    public float SteamPerSecond;

    [DataField, AutoNetworkedField]
    public bool Active;

    // How far the turn is, from nothing to done.
    [DataField, AutoNetworkedField]
    public float Progress;

    // Whether there is a deposit under the drill, for the window.
    [DataField, AutoNetworkedField]
    public bool OnDeposit;

    [ViewVariables]
    public float NextDirty;
}

// Key of the ore drill window.
[Serializable, NetSerializable]
public enum OreDrillUiKey : byte
{
    Key,
}

// What the appearance of an ore drill shows.
[Serializable, NetSerializable]
public enum DrillVisuals : byte
{
    Working,
}
