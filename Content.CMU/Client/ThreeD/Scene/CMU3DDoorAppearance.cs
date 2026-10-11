using Content.Shared.CMU14.ThreeD;
using Content.Shared.Doors.Components;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>DoorSystem owns flick timing and interruption; sample its actual Base frame.</summary>
public static class CMU3DDoorAppearance
{
    public static bool SupportedState(DoorState state) =>
        state is DoorState.Closed or DoorState.Open or DoorState.Opening or DoorState.Closing;

    public static bool TryParts(CMU3DModelPrototype model, DoorState doorState, CMU3DButtonLayer layer,
        bool extraVisibleLayer, out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (!SupportedState(doorState) || extraVisibleLayer || !layer.Visible || !layer.IdentityTransform ||
            layer.Color != Color.White || model.SourceDirections != 4 ||
            string.IsNullOrEmpty(model.ReferenceRsi) || layer.Rsi == null ||
            !CMU3DSourceReference.Matches(layer.Rsi, model.ReferenceRsi) ||
            layer.State is not ("closed" or "open" or "opening" or "closing") ||
            !model.DoorSpriteStates.TryGetValue(layer.State, out var state) ||
            layer.Frame < 0 || layer.Frame >= state.Frames.Count)
            return false;
        var frame = state.Frames[layer.Frame].Parts;
        if (frame.Count is < 1 or > 128)
            return false;
        foreach (var part in frame)
        {
            if (!part.Valid)
                return false;
        }
        // Collision phases can finish before the sprite flick. Do not force a stable
        // pose while the original owner is still displaying its final transition frame.
        parts = frame;
        key = $"{layer.State}:{layer.Frame}";
        return true;
    }
}
