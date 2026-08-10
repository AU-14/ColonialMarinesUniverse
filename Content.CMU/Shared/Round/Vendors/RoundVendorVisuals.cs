#nullable enable

using Robust.Shared.Serialization;

namespace Content.Shared.CMU.Round;

/// <summary>
/// Appearance data applied from an immutable round vendor profile.
/// </summary>
[Serializable, NetSerializable]
public enum RoundVendorVisuals : byte
{
    BaseRsi,
}
