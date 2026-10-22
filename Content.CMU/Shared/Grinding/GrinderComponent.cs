// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Grinding;

// A grinder: names of its slots (materials, outputs) and the state of the grind that is under way.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrinderComponent : Component
{
    public static readonly string[] MaterialSlotIds =
    {
        "material1", "material2", "material3",
        "material4", "material5", "material6",
        "material7", "material8", "material9",
    };

    public static readonly string[] OutputSlotIds =
    {
        "output1", "output2", "output3", "output4", "output5", "output6",
        "output7", "output8", "output9", "output10", "output11", "output12",
        "output13", "output14", "output15", "output16", "output17", "output18",
    };

    [DataField, AutoNetworkedField]
    public TimeSpan? GrindEndsAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? GrindDuration;

    [ViewVariables]
    public TimeSpan? GrindRemaining;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public TimeSpan GrindTime = TimeSpan.FromSeconds(3);

    // A steam grinder has no power: it runs on this many moles of water vapor every second of grinding.
    [DataField]
    public float SteamPerSecond;
}
