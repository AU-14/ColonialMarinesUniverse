using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Ghost;

[Serializable, NetSerializable]
public sealed class CMUSetGhostColorEvent(Color? color) : EntityEventArgs
{
    public readonly Color? Color = color;
}
