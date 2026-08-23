using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.ZLevels.Core;

[Serializable, NetSerializable]
public sealed partial class CMUZLevelLadderDoAfterEvent : SimpleDoAfterEvent
{
    public CMUZLevelLadderDoAfterEvent()
    {
    }

    public CMUZLevelLadderDoAfterEvent(int offset)
    {
        Offset = offset;
    }

    public int Offset { get; set; }
}
