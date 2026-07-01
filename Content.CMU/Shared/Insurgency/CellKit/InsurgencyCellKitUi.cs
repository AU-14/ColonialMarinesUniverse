using System;
using System.Collections.Generic;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.CellKit;

[Serializable, NetSerializable]
public enum InsurgencyCellKitUiKey : byte
{
    Key,
}

/// <summary>
///     Pushed to the cell kit UI: the prototype ids of the deployables still in the kit, in order,
///     plus the per-deploy do-after length so the client can show a matching progress hint.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyCellKitBuiState : BoundUserInterfaceState
{
    public List<string> Entries { get; }
    public float DeployTime { get; }

    public InsurgencyCellKitBuiState(List<string> entries, float deployTime)
    {
        Entries = entries;
        DeployTime = deployTime;
    }
}

/// <summary>
///     The player asked to deploy the entry at this index in the current list. The server re-checks
///     the index and runs the do-after before anything spawns.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyCellKitDeployMessage : BoundUserInterfaceMessage
{
    public int Index { get; }

    public InsurgencyCellKitDeployMessage(int index)
    {
        Index = index;
    }
}

/// <summary>
///     Do-after for placing one deployable. Carries the index so the completion handler knows which
///     entry to spawn and consume.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class InsurgencyCellKitDeployDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Index;

    private InsurgencyCellKitDeployDoAfterEvent()
    {
    }

    public InsurgencyCellKitDeployDoAfterEvent(int index)
    {
        Index = index;
    }

    public override DoAfterEvent Clone() => this;
}
