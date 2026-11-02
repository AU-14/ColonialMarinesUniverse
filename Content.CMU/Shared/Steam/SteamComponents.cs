// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Steam;

// A boiler: wood, coal or coke in its one fuel slot heats it, and the water that comes in through its pipe turns to steam that
// goes out through another. It lights a piece of fuel only when there is steam to be made.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoilerComponent : Component
{
    public const string FuelSlotId = "fuel";

    // The stacks that burn in it, and the seconds one piece of each keeps the fire going.
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, float> Fuels = new();

    // Water used up every second of a burning fire at full, and the moles of water vapor each unit of it makes.
    [DataField]
    public float WaterPerSecond = 2f;

    [DataField]
    public float SteamPerWater = 0.5f;

    // How much water the boiler keeps, and the pressure of the steam pipes (kPa) above which it makes no more steam.
    [DataField]
    public float WaterLimit = 100f;

    [DataField]
    public float MaxPressure = 300f;

    [DataField, AutoNetworkedField]
    public bool Burning;

    [ViewVariables]
    public float BurnLeft;
}

// Key of the boiler window.
[Serializable, NetSerializable]
public enum BoilerUiKey : byte
{
    Key,
}

// What the appearance of a boiler shows.
[Serializable, NetSerializable]
public enum BoilerVisuals : byte
{
    Burning,
}

// A generator turned by steam. It makes power while steam reaches it and stops when the steam runs out.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SteamGeneratorComponent : Component
{
    // Moles of water vapor used every second.
    [DataField]
    public float SteamPerSecond = 0.5f;

    [DataField, AutoNetworkedField]
    public bool Running;
}

// A pump: draws up the water it stands on, slowly, and gives it to the pipe; off the water it does nothing. The steel one works twice as fast
// and holds twice as much as the brass one: that is a matter of its prototype.
[RegisterComponent, NetworkedComponent]
public sealed partial class FluidPumpComponent : Component
{
    [DataField]
    public float WaterPerSecond = 2f;

    // Whether it stands on water, looked at again once a second.
    [ViewVariables]
    public bool OnWater;

    [ViewVariables]
    public float CheckIn;
}
