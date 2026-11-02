// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Cooking;

// A stove or an oven that heats the vessels (the pots, the pans, the pie moulds) put in it so that they cook what is in them. A stove of wood burns the fuel of its
// slot (firewood, planks, anything that a campfire takes) while its door is shut; an electric oven heats while it is powered and shut. The door has two states,
// and the slots can be reached only while it is open.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CookingStoveComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField, AutoNetworkedField]
    public bool Lit;

    // The slot of the fuel; none for the electric oven.
    [DataField]
    public string? FuelSlot = "fuel";

    // The slots of the vessels that it heats.
    [DataField]
    public List<string> CookerSlots = new() { "cooker" };

    // What burns in it and for how long one piece of it lasts, in seconds, by the type of the stack.
    [DataField]
    public Dictionary<string, float> Fuels = new();

    // The electric oven heats while it is powered and shut.
    [DataField]
    public bool Electric;

    // The fuel that is burning now, in seconds.
    [ViewVariables]
    public float FuelLeft;

    [ViewVariables]
    public TimeSpan NextTick;
}

// What the appearance of a cooking stove shows.
[Serializable, NetSerializable]
public enum StoveVisuals : byte
{
    Open,
    Lit,
}
