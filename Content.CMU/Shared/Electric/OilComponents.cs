// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Electric;

// A deposit of crude oil lying on the floor, to be pumped. How much it holds is drawn when it appears; the pump takes the oil out of
// it, and the deposit is gone when it is empty.
[RegisterComponent]
public sealed partial class OilDepositComponent : Component
{
    [DataField]
    public int MinTotal = 30000;

    [DataField]
    public int MaxTotal = 60000;

    [DataField]
    public int Remaining;

    [DataField]
    public bool Generated;
}

// An oil pump: set on a deposit with power, it pumps crude oil into its tank, to be sent on through a pipe.
[RegisterComponent, NetworkedComponent]
public sealed partial class OilPumpComponent : Component
{
    // Units of oil a second.
    [DataField]
    public float PerSecond = 10f;

    // How far from the pump a deposit can be.
    [DataField]
    public float Range = 0.75f;

    [DataField]
    public string Kind = "RUMCCrudeOil";
}

// A generator that burns a liquid fuel from its tank: it makes power for as long as there is fuel. The fuel is poured in from a
// canister, or comes through a pipe.
[RegisterComponent, NetworkedComponent]
public sealed partial class LiquidFuelGeneratorComponent : Component
{
    // The fuels it burns, and how many units of each a second: the first of them that the tank has is burnt. A cleaner fuel is a smaller
    // number. When this is empty the one Kind is burnt, at PerSecond.
    [DataField]
    public Dictionary<string, float> Fuels = new();

    [DataField]
    public string Kind = "RUMCDiesel";

    // Units of fuel a second.
    [DataField]
    public float PerSecond = 0.5f;

    [ViewVariables]
    public bool Running;
}
