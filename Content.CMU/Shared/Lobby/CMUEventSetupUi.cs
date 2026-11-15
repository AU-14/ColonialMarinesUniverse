using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Lobby;

[Serializable, NetSerializable]
public sealed record CMUEventSideInfo(string Id, string Name);

[Serializable, NetSerializable]
public sealed record CMUEventJobInfo(string Id, string Name, string Side);

[Serializable, NetSerializable]
public sealed record CMUEventGroupInfo(string Name, List<string> Jobs, int Players);

[Serializable, NetSerializable]
public sealed class CMUEventSetupState(
    bool enabled,
    bool inLobby,
    List<CMUEventSideInfo> sides,
    List<CMUEventJobInfo> jobs,
    List<CMUEventGroupInfo> groups,
    Dictionary<string, List<string>> slots,
    string feedback) : EuiStateBase
{
    public bool Enabled { get; } = enabled;
    public bool InLobby { get; } = inLobby;
    public List<CMUEventSideInfo> Sides { get; } = sides;
    public List<CMUEventJobInfo> Jobs { get; } = jobs;
    public List<CMUEventGroupInfo> Groups { get; } = groups;
    public Dictionary<string, List<string>> Slots { get; } = slots;
    public string Feedback { get; } = feedback;
}

[Serializable, NetSerializable]
public enum CMUEventSetupAction : byte
{
    Refresh,
    Enable,
    Disable,
    SaveGroup,
    RemoveGroup,
    AddSlots,
    SetSlots,
    ClearPending,
}

[Serializable, NetSerializable]
public sealed class CMUEventSetupMessage(
    CMUEventSetupAction action,
    string group = "",
    string code = "",
    string side = "",
    List<string>? roles = null,
    int amount = 0) : EuiMessageBase
{
    public CMUEventSetupAction Action { get; } = action;
    public string Group { get; } = group;
    public string Code { get; } = code;
    public string Side { get; } = side;
    public List<string> Roles { get; } = roles ?? [];
    public int Amount { get; } = amount;
}
