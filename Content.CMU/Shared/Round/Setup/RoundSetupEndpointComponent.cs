namespace Content.Shared.CMU.Round;

/// <summary>
/// Marks a mapper-facing endpoint by purpose while keeping round side and force selection separate.
/// </summary>
[RegisterComponent]
public sealed partial class RoundSetupEndpointComponent : Component
{
    /// <summary>
    /// Force-neutral purpose configured at this location.
    /// </summary>
    [DataField(required: true)]
    public RoundSetupSlot Slot { get; private set; }

    /// <summary>
    /// Explicit side for shared maps. Ship endpoints normally infer this from their owning grid.
    /// </summary>
    [DataField]
    public RoundSide? Side { get; private set; }
}

/// <summary>
/// Resolves the side of one semantic setup endpoint without consulting mutable force selection.
/// </summary>
public static class RoundSetupEndpointResolver
{
    /// <summary>
    /// Uses an explicit mapper side when present, otherwise the owning grid side, and rejects ambiguity.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither source provides a side or when the two sources conflict.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either source contains an unknown side.</exception>
    public static RoundSide ResolveSide(RoundSide? explicitSide, RoundSide? owningSide)
    {
        ValidateSide(explicitSide, nameof(explicitSide));
        ValidateSide(owningSide, nameof(owningSide));

        if (explicitSide is { } configured && owningSide is { } owner && configured != owner)
        {
            throw new InvalidOperationException(
                $"Round setup endpoint side '{configured}' conflicts with owning grid side '{owner}'.");
        }

        return explicitSide ?? owningSide ??
            throw new InvalidOperationException(
                "Round setup endpoint has neither an explicit side nor an owning side.");
    }

    private static void ValidateSide(RoundSide? side, string parameterName)
    {
        if (side is null or RoundSide.Govfor or RoundSide.Opfor)
            return;

        throw new ArgumentOutOfRangeException(parameterName, side, "Unknown round side.");
    }
}
