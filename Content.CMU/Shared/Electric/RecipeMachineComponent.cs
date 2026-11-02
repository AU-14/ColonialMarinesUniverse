// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Electric;

// A machine of the electric age. Every one of them is the same machine with a different Kind: it makes what the recipes of that
// kind (cmuMachineRecipe) say, out of the stacks in its input slots and the liquids in its tank, and gives the result into its
// output slots and tank. A recipe that names a circuit is made only while that circuit is set in the window, which is how one
// machine makes several things out of the same material.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RecipeMachineComponent : Component
{
    // The slots of an ordinary machine; one with more or fewer (the fabricator has eighteen to take and one to give) says how many.
    public static readonly string[] InputSlotIds = { "input1", "input2", "input3", "input4" };
    public static readonly string[] OutputSlotIds = { "output1", "output2", "output3", "output4" };

    // How many slots the machine takes materials in and gives its work out through: input1 ... inputN, output1 ... outputN.
    [DataField]
    public int InputSlots = 4;

    [DataField]
    public int OutputSlots = 4;

    private string[]? _inputIds;
    private string[]? _outputIds;

    public string[] InputIds => _inputIds ??= Ids("input", InputSlots);

    public string[] OutputIds => _outputIds ??= Ids("output", OutputSlots);

    private static string[] Ids(string prefix, int count)
    {
        var ids = new string[count];
        for (var i = 0; i < count; i++)
            ids[i] = prefix + (i + 1);

        return ids;
    }

    // Which recipes the machine makes: the value of "machine" in cmuMachineRecipe.
    [DataField(required: true)]
    public string Kind = string.Empty;

    // How much faster than the recipe says it works.
    [DataField]
    public float Speed = 1f;

    // The circuit set in the window; 0 is none.
    [DataField, AutoNetworkedField]
    public int Circuit;

    // A steam machine has no power: it works on this many moles of water vapor from its gas pipe every second of work.
    [DataField]
    public float SteamPerSecond;

    // The highest circuit the window can set.
    [DataField]
    public int MaxCircuit = 24;

    [DataField, AutoNetworkedField]
    public bool Active;

    // The operation that is going on and how far it is, from nothing to done.
    [DataField, AutoNetworkedField]
    public string? ActiveRecipe;

    [DataField, AutoNetworkedField]
    public float Progress;

    // The operation is done but there is no room for what it made.
    [DataField, AutoNetworkedField]
    public bool Blocked;

    [ViewVariables]
    public float NextDirty;
}

// What the machine looks like: the picture of a working machine is shown while it works.
[Serializable, NetSerializable]
public enum RecipeMachineVisuals : byte
{
    Working,
}

// Key of the window of a machine that makes things by recipe.
[Serializable, NetSerializable]
public enum RecipeMachineUiKey : byte
{
    Key,
}

// A circuit chosen in the window.
[Serializable, NetSerializable]
public sealed class RecipeMachineCircuitMessage(int circuit) : BoundUserInterfaceMessage
{
    public int Circuit = circuit;
}
