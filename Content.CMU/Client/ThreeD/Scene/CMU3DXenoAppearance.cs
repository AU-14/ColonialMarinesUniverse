using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Compose only the hive layers actually visible to this client, using their existing RSI clocks.</summary>
public static class CMU3DXenoAppearance
{
    public static bool TryParts(CMU3DModelPrototype model, IReadOnlyList<CMU3DButtonLayer> layers,
        out IReadOnlyList<CMU3DModelPart> parts)
    {
        parts = [];
        var result = new List<CMU3DModelPart>();
        foreach (var layer in layers)
        {
            if (!layer.Visible || layer.Color.A <= 0)
                continue;
            if (!layer.IdentityTransform || layer.Rsi == null || layer.State == null)
                return false;
            CMU3DSpriteState? state = null;
            foreach (var (rsi, states) in model.XenoStates)
            {
                if (CMU3DSourceReference.Matches(layer.Rsi, rsi))
                {
                    states.TryGetValue(layer.State, out state);
                    break;
                }
            }
            if (state == null || layer.Frame < 0 || layer.Frame >= state.Frames.Count)
                return false;
            foreach (var part in state.Frames[layer.Frame].Parts)
            {
                if (!part.Valid || result.Count >= 128)
                    return false;
                if (layer.Color == Color.White)
                    result.Add(part);
                else
                    result.Add(new CMU3DModelPart
                    {
                        Label = part.Label, Shape = part.Shape, Min = part.Min, Max = part.Max,
                        Yaw = part.Yaw, Pitch = part.Pitch, Color = part.Color * layer.Color,
                        Surface = part.Surface, SurfaceAxis = part.SurfaceAxis, SurfaceFlipU = part.SurfaceFlipU,
                    });
            }
        }
        parts = result;
        return true;
    }
}
