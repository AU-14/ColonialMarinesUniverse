// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Steam;

// A condenser: a liquid comes in at one side and the liquid that cools it at the other, and with a material in its slot the
// liquid sets into solid pieces. What it makes of which liquids is written down in the condenser recipes. The ordinary condenser
// runs on the grid; the brass one needs no power but must be given a steady supply of the coolant.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CondenserComponent : Component
{
    public const string MaterialSlotId = "material";
    public const string OutputSlotId = "output";

    // Whether it runs on the grid.
    [DataField]
    public bool NeedsPower = true;

    // The least of the coolant that has to be in it all the time it works (zero for none): when the supply falls short
    // the work is lost.
    [DataField]
    public float SteadyCoolant;

    [DataField, AutoNetworkedField]
    public bool Active;

    // How far the piece that is being made is, from nothing to done.
    [DataField, AutoNetworkedField]
    public float Progress;

    [ViewVariables]
    public string? ActiveRecipe;

    [ViewVariables]
    public float NextDirty;
}

// Key of the condenser window.
[Serializable, NetSerializable]
public enum CondenserUiKey : byte
{
    Key,
}

// What a condenser makes: this much of the liquid, cooled with this much of the coolant, and a piece of the material, set into
// the result.
[Prototype("cmuCondenserRecipe")]
public sealed partial class CondenserRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // The liquid that is condensed, as a reagent, and how much of it a piece takes.
    [DataField(required: true)]
    public string Liquid = string.Empty;

    [DataField]
    public float LiquidAmount = 10f;

    // The liquid that cools it.
    [DataField(required: true)]
    public string Coolant = string.Empty;

    [DataField]
    public float CoolantAmount = 5f;

    // The material in the slot, and how much of it a piece uses up; a recipe that needs none (the one of slag) leaves it out.
    [DataField]
    public ProtoId<StackPrototype>? Material;

    [DataField]
    public int MaterialAmount = 1;

    [DataField(required: true)]
    public ProtoId<StackPrototype> Result = default!;

    [DataField]
    public int ResultAmount = 1;

    // Seconds of work.
    [DataField]
    public float Duration = 4f;
}
