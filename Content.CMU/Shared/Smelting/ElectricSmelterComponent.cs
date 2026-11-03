// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Smelting;

// The electric smelter: names of its slots and its target and current temperature, and how fast it heats.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ElectricSmelterComponent : Component
{
    public static readonly string[] MaterialSlotIds = { "material1", "material2", "material3", "material4" };

    public const string OutputSlotId = "output";

    [DataField]
    public float MinTemperature = 20f;

    [DataField]
    public float MaxTemperature = 2000f;

    [DataField, AutoNetworkedField]
    public float TargetTemperature = 20f;

    [DataField, AutoNetworkedField]
    public float CurrentTemperature = 20f;

    [DataField]
    public float HeatRatePerSecond = 25f;

    [DataField, AutoNetworkedField]
    public TimeSpan? BatchEndsAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? BatchDuration;

    [ViewVariables]
    public TimeSpan? BatchRemaining;

    [ViewVariables]
    public string? RecipeId;

    [DataField, AutoNetworkedField]
    public bool Active;
}
