// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Forge;

// A casting mold: takes molten metal, cools it for a while, and then gives the cast item.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MoldComponent : Component
{
    [DataField]
    public string Solution = "cast";

    [DataField]
    public FixedPoint2 Capacity = FixedPoint2.New(150);

    [DataField]
    public List<MoldRecipe> Recipes = new();

    [DataField, AutoNetworkedField]
    public bool Cooling;

    [DataField, AutoNetworkedField]
    public bool Ready;

    [ViewVariables]
    public TimeSpan? SolidifiesAt;

    [ViewVariables]
    public MoldRecipe? CoolingRecipe;
}

// A casting recipe: which molten reagents are needed, what comes out and how long it cools.
[DataDefinition]
public sealed partial class MoldRecipe
{
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Ingredients = new();

    [DataField(required: true)]
    public EntProtoId Output;

    [DataField]
    public int OutputCount = 1;

    [DataField]
    public TimeSpan CoolTime = TimeSpan.FromSeconds(6);

    [DataField(required: true)]
    public string Name = string.Empty;
}

// What the appearance of a mold shows.
[Serializable, NetSerializable]
public enum MoldVisuals : byte
{
    State,
}

// The look of a mold: empty, filled, cooling or ready.
[Serializable, NetSerializable]
public enum MoldSpriteState : byte
{
    Empty,
    Cooling,
    Ready,
}
