using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Use the current source RSI frame without replacing its timing, fade owner or unknown layers.</summary>
public static class CMU3DSpriteAppearance
{
    /// <summary>Keep rotating-source support explicit; unknown source rendering still uses the sprite.</summary>
    public static bool SupportsRotation(CMU3DModelPrototype model, bool noRotation, bool snapCardinals,
        bool specialRendering)
    {
        var opening = model.FloorOpening != null || model.CeilingOpening != null;
        if (model.SourceSpriteRotates)
            return !opening && model.SpriteStates.Count > 0 && !noRotation && !snapCardinals && !specialRendering;
        return noRotation || opening;
    }

    public static bool TryParts(CMU3DModelPrototype model, CMU3DButtonLayer layer,
        Vector2 offset, Vector2 scale, float rotation, int directions, int sourceFrameCount, bool extraVisibleLayer,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (model.BakedSpriteTint != Color.White || extraVisibleLayer || offset != model.SourceSpriteOffset || scale != Vector2.One || rotation != 0 ||
            !layer.Visible || !layer.IdentityTransform || layer.Color != Color.White ||
            model.SourceDirections is not (1 or 4) || directions != model.SourceDirections ||
            model.SourceDirections == 4 && (model.ReferenceDirection is not (>= 0 and < 4) || model.DirectionalModels.Length != 4) ||
            string.IsNullOrEmpty(model.ReferenceRsi) || layer.Rsi == null ||
            !CMU3DSourceReference.Matches(layer.Rsi, model.ReferenceRsi) ||
            layer.State == null || model.SpriteStates.Count is < 1 or > 32 ||
            !model.SpriteStates.TryGetValue(layer.State, out var state) ||
            state.Frames.Count is < 1 or > 64 || state.Delays.Count != state.Frames.Count || sourceFrameCount != state.Frames.Count ||
            layer.Frame < 0 || layer.Frame >= state.Frames.Count)
            return false;
        foreach (var delay in state.Delays)
        {
            if (!float.IsFinite(delay) || delay <= 0)
                return false;
        }
        var frame = state.Frames[layer.Frame].Parts;
        if (frame.Count is < 1 or > 128)
            return false;
        foreach (var part in frame)
        {
            if (!part.Valid)
                return false;
        }
        parts = frame;
        key = $"{layer.State}:{layer.Frame}";
        return true;
    }
}
