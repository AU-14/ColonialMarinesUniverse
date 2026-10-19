using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.DropPod;

[Serializable, NetSerializable]
public enum CMUDropPodConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed record CMUDropPodEntry(NetEntity Pod, string Name, string? Occupant, int Weapons, float Radius, bool Custom, bool Ready);

[Serializable, NetSerializable]
public sealed class CMUDropPodConsoleBuiState(List<CMUDropPodEntry> pods, Vector2i? target, TimeSpan nextLaunch) : BoundUserInterfaceState
{
    public readonly List<CMUDropPodEntry> Pods = pods;
    public readonly Vector2i? Target = target;
    public readonly TimeSpan NextLaunch = nextLaunch;
}

[Serializable, NetSerializable]
public sealed class CMUDropPodSelectTargetMsg(Vector2i target) : BoundUserInterfaceMessage
{
    public readonly Vector2i Target = target;
}

[Serializable, NetSerializable]
public sealed class CMUDropPodLaunchMsg(List<NetEntity> pods) : BoundUserInterfaceMessage
{
    public readonly List<NetEntity> Pods = pods;
}

[Serializable, NetSerializable]
public sealed class CMUDropPodRefreshMsg : BoundUserInterfaceMessage;
