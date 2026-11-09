using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Roles;

namespace Content.Shared.CMU14.Lobby;

[Serializable, NetSerializable]
public sealed class CMUEventCodeRequestEvent(string code) : EntityEventArgs
{
    public const int MaxCodeLength = 128;
    public string Code { get; } = code;
}

/// <summary>Only the recipient's locked jobs are sent; access codes stay on the server.</summary>
[Serializable, NetSerializable]
public sealed class CMUEventAccessStateEvent(bool enabled, HashSet<ProtoId<JobPrototype>> lockedJobs) : EntityEventArgs
{
    public bool Enabled { get; } = enabled;
    public HashSet<ProtoId<JobPrototype>> LockedJobs { get; } = lockedJobs;
}

[Serializable, NetSerializable]
public sealed class CMUEventCodeResultEvent(string message) : EntityEventArgs
{
    public string Message { get; } = message;
}
