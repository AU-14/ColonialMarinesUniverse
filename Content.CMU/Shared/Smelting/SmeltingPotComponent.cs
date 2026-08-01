// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._AU14.Smelting;

/// <summary>
/// A crucible that renders ore into sheets over a lit fire.
///
/// Design notes, because they explain most of the shape of this component:
///  * The pot holds ONE material at a time. That is what removes the need for any recipe picker: what you put
///    in decides what it is smelting, so there is no mode to set and no mode to set wrongly.
///  * Finished sheets land in a separate OUTPUT buffer and are never fed back in, so an ore load cannot run
///    away into plasteel on its own. Refining a second step is a deliberate act: take the sheets out, put
///    them back in.
///  * Capacity is counted in MATERIAL UNITS, not entities - ore is a stack type, so one entity can be thirty
///    ore and an entity cap would mean nothing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SmeltingPotComponent : Component
{
    /// <summary>🔧 TUNABLE: total input units the pot can hold.</summary>
    [DataField]
    public int Capacity = 100;

    /// <summary>What this pot knows how to smelt. First match on the inserted stack type wins.</summary>
    [DataField]
    public List<SmeltingRecipe> Recipes = new();

    /// <summary>Stack type currently loaded, or null when empty (or when holding electronics, which are not a
    /// stack). Also decides what further inserts are allowed.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<StackPrototype>? Material;

    /// <summary>True when the load is scrap electronics rather than a stack material. Circuit boards are
    /// discrete entities with no stack type, so they need their own flag rather than a <see cref="Material"/>.</summary>
    [DataField, AutoNetworkedField]
    public bool Electronics;

    /// <summary>Pip sprite used while the pot holds electronics, since there is no stack icon to borrow.</summary>
    [DataField]
    public SpriteSpecifier? ElectronicsIcon;

    /// <summary>Input units currently loaded.</summary>
    [DataField, AutoNetworkedField]
    public int Amount;

    /// <summary>Finished sheets waiting to be collected, and what they are.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<StackPrototype>? OutputMaterial;

    [DataField, AutoNetworkedField]
    public int OutputAmount;

    /// <summary>🔧 TUNABLE: how many finished sheets may pile up before smelting stalls.</summary>
    [DataField]
    public int OutputCapacity = 100;

    /// <summary>True while sitting on a lit fire with a batch in progress. Drives sprite, steam and sound.</summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>The fire this pot is sitting on, if any. Server-side: the client only needs <see cref="Active"/>.</summary>
    [ViewVariables]
    public EntityUid? Fire;

    /// <summary>When the batch in progress completes. Cleared when the fire goes out, preserving progress via
    /// <see cref="BatchRemaining"/>.</summary>
    [ViewVariables]
    public TimeSpan? BatchEndsAt;

    /// <summary>Time left on the interrupted batch, so a fire going out pauses rather than discards progress.</summary>
    [ViewVariables]
    public TimeSpan? BatchRemaining;
}

/// <summary>One input stack type -> output stack type conversion.</summary>
[DataDefinition]
public sealed partial class SmeltingRecipe
{
    /// <summary>
    /// Input stack type. Null for an electronics recipe, which matches on components instead of a stack.
    ///
    /// Nullable on purpose: the electronics recipe leaves this unset, and a non-nullable ProtoId serialises
    /// that as null, which then throws NullNotAllowedException when the prototype is read back. That is what
    /// broke ServerPrototypeSaveLoadSaveTest / ClientPrototypeSaveLoadSaveTest.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype>? Input;

    /// <summary>Matches any circuit board instead of a stack type - the same "any electronics" rule the
    /// sapper workbench uses (machine board, computer board or door electronics).</summary>
    [DataField]
    public bool InputAnyElectronics;

    /// <summary>Input units consumed per batch.</summary>
    [DataField]
    public int InputAmount = 2;

    [DataField(required: true)]
    public ProtoId<StackPrototype> Output;

    /// <summary>Sheets produced per batch.</summary>
    [DataField]
    public int OutputAmount = 1;

    /// <summary>🔧 TUNABLE: how long one batch takes over a lit fire.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(4);
}

[Serializable, NetSerializable]
public enum SmeltingPotVisuals : byte
{
    Active,
}
