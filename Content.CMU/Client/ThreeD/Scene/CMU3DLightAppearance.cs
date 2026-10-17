using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Follow the light visualizer's actual base layer, including its Off/On blink frames.</summary>
public static class CMU3DLightAppearance
{
    public static bool TryParts(CMU3DModelPrototype model, CMU3DButtonLayer layer, bool extraVisibleLayer,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (extraVisibleLayer || !layer.Visible || !layer.IdentityTransform || layer.Color != Color.White ||
            layer.Frame != 0 || layer.State == null || model.SourceDirections != 4 || !model.WallMounted ||
            string.IsNullOrEmpty(model.ReferenceRsi) || layer.Rsi == null ||
            !CMU3DSourceReference.Matches(layer.Rsi, model.ReferenceRsi) ||
            !model.PoweredLightStates.TryGetValue(layer.State, out var state) || state.Parts.Count is < 1 or > 128)
            return false;
        foreach (var part in state.Parts)
        {
            if (!part.Valid)
                return false;
        }
        parts = state.Parts;
        key = layer.State;
        return true;
    }
}
