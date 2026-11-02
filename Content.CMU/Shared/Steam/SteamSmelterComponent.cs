// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Steam;

// A smelter driven by steam. It has no thermostat, and no recipes of its own: ore and a mold of the right shape turn into ingots,
// as the recipes of the steam smelter say. Every operation leaves slag, which goes away through a pipe at the pace the slag
// regulator allows.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SteamSmelterComponent : Component
{
    public static readonly string[] MaterialSlotIds = { "material1", "material2", "material3", "material4" };

    public const string MoldSlotId = "mold";
    public const string OutputSlotId = "output";

    // Moles of water vapor used every second at full regulator.
    [DataField]
    public float SteamPerSecond = 0.4f;

    // How much slag the smelter keeps before it must stop (each recipe says how much one operation leaves).
    [DataField]
    public float SlagLimit = 45f;

    // Whether an operation leaves slag at all. The industrial smelter does not: it melts the ore clean, and has no slag to let out.
    [DataField]
    public bool ProducesSlag = true;

    // The steam regulator sets how fast it works (and how much steam it uses), the slag regulator how fast the slag is let out.
    [DataField, AutoNetworkedField]
    public float SteamRegulator = 1f;

    [DataField, AutoNetworkedField]
    public float SlagRegulator = 1f;

    [DataField]
    public float MinSteamRegulator = 0.2f;

    [DataField, AutoNetworkedField]
    public bool Active;

    // How far the current operation is, from nothing to done.
    [DataField, AutoNetworkedField]
    public float Progress;

    [ViewVariables]
    public string? ActiveRecipe;

    [ViewVariables]
    public float NextDirty;
}

// Key of the steam smelter window.
[Serializable, NetSerializable]
public enum SteamSmelterUiKey : byte
{
    Key,
}

// A change of one of the two regulators from the window.
[Serializable, NetSerializable]
public sealed class SteamSmelterRegulatorMessage(bool slag, float value) : BoundUserInterfaceMessage
{
    public bool Slag = slag;
    public float Value = value;
}
