using Robust.Shared.Maths;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     One plastic colour for the chat housing: its label ink and its keycaps. The housing and screen
///     themselves are textures in <c>Textures/CMU14/Interface/ChatHousing/</c>, generated to match.
/// </summary>
public readonly record struct CmuHousingTone(
    string Id,
    Color Ink,
    Color InkDim,
    Color KeyFill,
    Color KeyHover,
    Color KeyPressed,
    Color KeyHighlight,
    Color KeyShadow,
    Color KeyInk);

public static class CmuHousingPalette
{
    public const string Off = "off";

    private static readonly CmuHousingTone[] Tones =
    {
        new("gunmetal",
            Color.FromHex("#b9b6a8"), Color.FromHex("#7c7c70"),
            Color.FromHex("#40433b"), Color.FromHex("#4c5046"), Color.FromHex("#2a2c26"),
            Color.FromHex("#6c7065"), Color.FromHex("#141512"), Color.FromHex("#dcd8c9")),
        new("olive",
            Color.FromHex("#c2bf9e"), Color.FromHex("#858263"),
            Color.FromHex("#454931"), Color.FromHex("#51563a"), Color.FromHex("#2d301f"),
            Color.FromHex("#6f7452"), Color.FromHex("#15160d"), Color.FromHex("#e2dec2")),
        new("beige",
            Color.FromHex("#2d2a22"), Color.FromHex("#5a5545"),
            Color.FromHex("#d3ccb6"), Color.FromHex("#e0d9c4"), Color.FromHex("#b4ac94"),
            Color.FromHex("#f5efdc"), Color.FromHex("#6f6856"), Color.FromHex("#2a2720")),
    };

    public static bool TryGet(string id, out CmuHousingTone tone)
    {
        foreach (var candidate in Tones)
        {
            if (candidate.Id != id)
                continue;

            tone = candidate;
            return true;
        }

        tone = default;
        return false;
    }
}
