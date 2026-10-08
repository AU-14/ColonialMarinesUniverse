using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Samples four actual sprite layers; owns no solution mixing, fill thresholds or activation timer.</summary>
public static class CMU3DReagentTankAppearance
{
    public const string Rsi = "_RMC14/Structures/Storage/reagent_tank.rsi";

    public static bool TryParts(CMU3DModelPrototype model, IReadOnlyList<CMU3DButtonLayer> layers,
        int fillLayer, out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (model.ReagentTankAppearance is not { } definition || model.ReferenceRsi != Rsi ||
            model.ReferenceState != "tank_normal" || model.SourceDirections != 1 ||
            model.BakedSpriteTint != Color.White || layers.Count != 4 || fillLayer != 2 ||
            definition.VesselParts.Length is < 1 or > 128 || model.Parts.Count is < 1 or > 128)
            return false;
        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var expected = i switch { 0 => "tank_normal", 1 => "tn_color-1", 3 => "t_inactive", _ => null };
            if (layer.Rsi?.TrimStart('/') != "Textures/" + Rsi || !layer.IdentityTransform || layer.Frame != 0 ||
                !ValidColor(layer.Color) || (i == 2 ? layer.State is not ("tn_color-1" or "tn_color-2") :
                    layer.State != expected || !layer.Visible || layer.Color != Color.White))
                return false;
        }
        var labels = new HashSet<string>();
        foreach (var label in definition.VesselParts)
        {
            if (string.IsNullOrEmpty(label) || !labels.Add(label))
                return false;
            var matches = 0;
            foreach (var part in model.Parts)
                if (part.Label == label)
                {
                    if (part.Color != Color.White)
                        return false;
                    matches++;
                }
            if (matches != 1)
                return false;
        }
        foreach (var part in model.Parts)
            if (!part.Valid || !ValidColor(part.Color))
                return false;
        var fill = layers[2];
        // Both source masks are byte-identical and binary-alpha (validated at asset build).
        // Source-over an opaque white vessel is this opaque multiplier; no coincident fill solids.
        var alpha = fill.Visible ? fill.Color.A : 0;
        var tint = new Color(1 - alpha + alpha * fill.Color.R, 1 - alpha + alpha * fill.Color.G,
            1 - alpha + alpha * fill.Color.B, 1);
        if (tint == Color.White)
            parts = model.Parts;
        else
        {
            var result = new CMU3DModelPart[model.Parts.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var part = model.Parts[i];
                result[i] = !labels.Contains(part.Label) ? part : new CMU3DModelPart
                {
                    Label = part.Label, Min = part.Min, Max = part.Max, Shape = part.Shape,
                    Yaw = part.Yaw, Pitch = part.Pitch, Surface = part.Surface, SurfaceAxis = part.SurfaceAxis,
                    SurfaceFlipU = part.SurfaceFlipU, OmitWhenConnected = part.OmitWhenConnected, Color = tint,
                };
            }
            parts = result;
        }
        key = $"reagent-tank:{fill.State}:{fill.Visible}:{fill.Color.ToHex()}";
        return true;
    }

    public static bool ValidColor(Color color) =>
        float.IsFinite(color.R) && color.R is >= 0 and <= 1 &&
        float.IsFinite(color.G) && color.G is >= 0 and <= 1 &&
        float.IsFinite(color.B) && color.B is >= 0 and <= 1 &&
        float.IsFinite(color.A) && color.A is >= 0 and <= 1;

    // Overall alpha is applied separately to each original layer before blending.
    // A faded multi-layer sprite cannot use the opaque-mask consolidation proof.
    public static bool ValidSpriteColor(Color color) => ValidColor(color) && color.A == 1;
}
