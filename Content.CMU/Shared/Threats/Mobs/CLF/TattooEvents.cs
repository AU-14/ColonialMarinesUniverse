using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.CLF;

/// <summary>
/// Raised on the target entity when they confirm the CLF tattoo dialog.
/// </summary>
[Serializable, NetSerializable]
public sealed class TattooAcceptEvent : EntityEventArgs
{
}

/// <summary>
/// DoAfter event for the tattooing process.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class TattooDoAfterEvent : SimpleDoAfterEvent
{
}

