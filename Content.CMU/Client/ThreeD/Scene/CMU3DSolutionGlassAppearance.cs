using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Reads source layers only. Solution volume, mixing and vessel changes remain source-owned.</summary>
public static class CMU3DSolutionGlassAppearance
{
    public static bool TryParts(CMU3DModelPrototype model, IReadOnlyList<CMU3DButtonLayer> layers,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (model.SolutionAppearance is not { } definition || layers.Count is < 2 or > 3 ||
            model.SourceDirections != 1 || !model.UseEntityRotation || model.Placement != "surface" ||
            model.ReferenceState != "icon" || model.BakedSpriteTint != Color.White ||
            model.ChargerAppearance != null || model.ReagentTankAppearance != null || model.FoamAppearance != null ||
            model.ReferenceRsi is not ("Objects/Consumable/Drinks/glass_clear.rsi" or "_RMC14/Objects/Consumable/Drinks/drink_glass.rsi") ||
            layers.Count != (model.ReferenceRsi == "Objects/Consumable/Drinks/glass_clear.rsi" ? 3 : 2) ||
            definition.Layers.Count is < 2 or > 64 || !layers[0].Visible)
            return false;
        List<CMU3DModelPart> result = [];
        var identityKey = string.Empty;
        var identity = new HashSet<string>();
        foreach (var entry in definition.Layers)
            if (entry.Role is not ("Base" or "Fill" or "Overlay") || string.IsNullOrEmpty(entry.Rsi) ||
                string.IsNullOrEmpty(entry.State) || !identity.Add($"{entry.Role}:{entry.Rsi}:{entry.State}"))
                return false;
        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            var role = index switch { 0 => "Base", 1 => "Fill", _ => "Overlay" };
            if (!layer.IdentityTransform || layer.Frame != 0 || !CMU3DReagentTankAppearance.ValidColor(layer.Color))
                return false;
            CMU3DSolutionLayerGeometry? selected = null;
            foreach (var entry in definition.Layers)
                if (entry.Role == role && layer.Rsi?.TrimStart('/') == "Textures/" + entry.Rsi && entry.State == layer.State)
                    selected = entry;
            if (selected == null)
                return false;
            identityKey += $"{role}:{selected.Rsi}:{selected.State}:{layer.Visible}:{layer.Color.ToHex()};";
            if (!layer.Visible)
                continue;
            foreach (var part in selected.Parts)
            {
                if (!part.Valid || !CMU3DReagentTankAppearance.ValidColor(part.Color) || result.Count >= 128)
                    return false;
                var tint = new Color(part.Color.R * layer.Color.R, part.Color.G * layer.Color.G,
                    part.Color.B * layer.Color.B, part.Color.A * layer.Color.A);
                result.Add(new CMU3DModelPart
                {
                    Label = part.Label, Min = part.Min, Max = part.Max, Shape = part.Shape,
                    Yaw = part.Yaw, Pitch = part.Pitch, Surface = part.Surface, SurfaceAxis = part.SurfaceAxis,
                    SurfaceFlipU = part.SurfaceFlipU, OmitWhenConnected = part.OmitWhenConnected, Color = tint,
                });
            }
        }
        if (result.Count == 0)
            return false;
        parts = result;
        key = identityKey;
        return true;
    }
}
