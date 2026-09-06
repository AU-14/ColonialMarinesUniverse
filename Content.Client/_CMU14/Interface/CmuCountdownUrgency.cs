using Content.Client.Stylesheets;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     How urgent a countdown reads, from seconds remaining alone - shared so every timer that
///     escalates colour as it runs out agrees on when.
/// </summary>
/// <remarks>
///     Split out of <c>LobbyState</c>, which owned these two thresholds alone until the vote popup
///     needed the same escalation for a much shorter countdown. Two independent copies of 60/20 is
///     how they quietly drift apart the next time either gets retuned - this is the one place that
///     number lives now, and both callers read it.
/// </remarks>
public static class CmuCountdownUrgency
{
    public enum Level
    {
        Normal,
        Soon,
        Imminent,
    }

    /// <summary>Below this many seconds left, a countdown is worth noticing.</summary>
    public const double SoonSeconds = 60;

    /// <summary>Below this many seconds left, a countdown is worth acting on.</summary>
    public const double ImminentSeconds = 20;

    public static Level Get(double secondsLeft)
    {
        if (secondsLeft <= ImminentSeconds)
            return Level.Imminent;

        return secondsLeft <= SoonSeconds ? Level.Soon : Level.Normal;
    }

    /// <summary>
    ///     The colour a plain text countdown should use. For a caller that doesn't want to swap a
    ///     whole style class - and with it the font, the way the round clock does - setting
    ///     <c>Label.FontColorOverride</c> to this is the same three hues without the class-tie risk
    ///     of stacking a second class on a label that already carries one for its size.
    /// </summary>
    public static Color GetColor(double secondsLeft)
    {
        return Get(secondsLeft) switch
        {
            Level.Imminent => StyleNano.CrtDanger,
            Level.Soon => StyleNano.CrtWarning,
            _ => CrtTerminalPalette.TextBright,
        };
    }
}
