#nullable enable

namespace Content.Shared.CMU.Round;

/// <summary>
/// The military side a force represents during a round.
/// </summary>
public enum RoundSide : byte
{
    Govfor,
    Opfor,
}

/// <summary>
/// Stable identity of a selectable military force.
/// </summary>
public readonly record struct RoundForceId
{
    public RoundForceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}

/// <summary>
/// Immutable assignment of a military force and main ship to one round side.
/// </summary>
public readonly record struct RoundForceAssignment(
    RoundSide Side,
    RoundForceId Force,
    string? MainShipId);
