using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Ops.ThirdParty;

[Serializable, NetSerializable]
public sealed class CMUCallInCharacterState(string role, Dictionary<int, string> characters, int seconds) : EuiStateBase
{
    public readonly string Role = role;
    public readonly Dictionary<int, string> Characters = characters;
    public readonly int Seconds = seconds;
}

[Serializable, NetSerializable]
public sealed class CMUCallInCharacterChoice(int? slot) : EuiMessageBase
{
    public readonly int? Slot = slot;
}
