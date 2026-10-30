using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Pointing;

/// <summary>
///     Ported from cmss13 PR 12840. Sent only to the pointer's squad (or faction, without a squad): outline what their
///     leader just pointed at.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUPointOutlineEvent(NetEntity target, Color color, float width, TimeSpan duration) : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly Color Color = color;
    public readonly float Width = width;
    public readonly TimeSpan Duration = duration;
}
