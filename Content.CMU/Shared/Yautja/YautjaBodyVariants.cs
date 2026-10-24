using Content.Shared.Humanoid;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// CMU14: CM-SS13 keeps the Yautja body as six pre-coloured variants per part in one sheet, ported here as
/// "&lt;part&gt;_&lt;colour&gt;" states in CMU14/Yautja/body.rsi. This is the single place that decides whether a sprite
/// belongs to that sheet, so the in-game renderer and the lobby preview agree.
/// </summary>
public static class YautjaBodyVariants
{
    public const string BodyRsi = "CMU14/Yautja/body.rsi";

    public static bool IsBodyRsi(string? rsiPath) =>
        rsiPath != null && rsiPath.Contains(BodyRsi, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pre-coloured state for a body layer, or null when the layer is not part of the sheet (eyes, hair, ...). A missing
    /// organ simply never reaches this method, and a removed layer is rejected by the renderer's layer lookup, so
    /// delimbing needs no special handling here.
    /// </summary>
    public static string? StateFor(HumanoidVisualLayers layer, Sex sex, string suffix)
    {
        var male = sex != Sex.Female;
        return layer switch
        {
            HumanoidVisualLayers.Head => $"head_{(male ? "m" : "f")}_{suffix}",
            HumanoidVisualLayers.Chest => $"torso_{(male ? "m" : "f")}_{suffix}",
            HumanoidVisualLayers.LArm => $"l_arm_{suffix}",
            HumanoidVisualLayers.RArm => $"r_arm_{suffix}",
            HumanoidVisualLayers.LHand => $"l_hand_{suffix}",
            HumanoidVisualLayers.RHand => $"r_hand_{suffix}",
            HumanoidVisualLayers.LLeg => $"l_leg_{suffix}",
            HumanoidVisualLayers.RLeg => $"r_leg_{suffix}",
            HumanoidVisualLayers.LFoot => $"l_foot_{suffix}",
            HumanoidVisualLayers.RFoot => $"r_foot_{suffix}",
            _ => null,
        };
    }

    public static string Suffix(YautjaSkinColor colour) => colour switch
    {
        YautjaSkinColor.Green => "green",
        YautjaSkinColor.Purple => "purple",
        YautjaSkinColor.Blue => "blue",
        YautjaSkinColor.Red => "red",
        YautjaSkinColor.Black => "black",
        _ => "tan",
    };
}
