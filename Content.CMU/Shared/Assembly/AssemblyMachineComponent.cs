// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Assembly;

// An assembly machine: names of its slots (materials, output, blueprint, upgrades) and the state of the batch it is making.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssemblyMachineComponent : Component
{
    public static readonly string[] MaterialSlotIds =
    {
        "material1", "material2", "material3",
        "material4", "material5", "material6",
        "material7", "material8", "material9",
    };

    public const string OutputSlotId = "output";
    public const string BlueprintSlotId = "blueprint";
    public static readonly string[] UpgradeSlotIds = { "upgrade1", "upgrade2" };

    [DataField, AutoNetworkedField]
    public TimeSpan? AssembleEndsAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? AssembleDuration;

    [DataField, AutoNetworkedField]
    public bool Active;

    [ViewVariables]
    public string? PendingRecipe;
}

// What the appearance of an assembly machine shows.
[Serializable, NetSerializable]
public enum AssemblyMachineVisuals : byte
{
    Working,
}
