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

    /// <summary>
    /// Whether this value contains a usable force identifier.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}

/// <summary>
/// Immutable assignment of a military force and main ship to one round side.
/// </summary>
public readonly record struct RoundForceAssignment
{
    private readonly RoundSide _side;
    private readonly RoundForceId _force;

    public RoundForceAssignment(
        RoundSide side,
        RoundForceId force,
        string? mainShipId)
    {
        ValidateSide(side, nameof(side));
        ValidateForce(force, nameof(force));

        _side = side;
        _force = force;
        MainShipId = mainShipId;
    }

    public RoundSide Side
    {
        get => _side;
        init
        {
            ValidateSide(value, nameof(value));
            _side = value;
        }
    }

    public RoundForceId Force
    {
        get => _force;
        init
        {
            ValidateForce(value, nameof(value));
            _force = value;
        }
    }

    public string? MainShipId { get; init; }

    private static void ValidateSide(RoundSide side, string parameterName)
    {
        if (side is RoundSide.Govfor or RoundSide.Opfor)
            return;

        throw new ArgumentOutOfRangeException(parameterName, side, "Unknown round side.");
    }

    private static void ValidateForce(RoundForceId force, string parameterName)
    {
        if (force.IsValid)
            return;

        throw new ArgumentException("The round force identifier cannot be missing.", parameterName);
    }
}
