using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Read the current 2D owner's frame; never invent a timer or silently replace an unknown appearance.</summary>
public static class CMU3DDoorButtonAppearance
{
    public static bool TryParts(CMU3DModelPrototype model, CMU3DButtonLayer animation, CMU3DButtonLayer power,
        bool extraVisibleLayer, out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (extraVisibleLayer || !animation.Visible || !ValidLayer(model, animation) ||
            animation.State == null || !model.DoorButtonStates.TryGetValue(animation.State, out var state) ||
            animation.Frame < 0 || animation.Frame >= state.Frames.Count ||
            state.Frames.Count != state.UnpoweredFrames.Count)
            return false;
        if (power.Visible && (!ValidLayer(model, power) || power.State != "doorctrl-p" || power.Frame != 0))
            return false;
        parts = (power.Visible ? state.UnpoweredFrames : state.Frames)[animation.Frame].Parts;
        if (parts.Count is < 1 or > 128)
            return false;
        foreach (var part in parts)
        {
            if (!part.Valid)
                return false;
        }
        key = $"{animation.State}:{animation.Frame}:{(power.Visible ? "unpowered" : "powered")}";
        return true;
    }

    private static bool ValidLayer(CMU3DModelPrototype model, CMU3DButtonLayer layer) =>
        model.SourceDirections == 1 && !string.IsNullOrEmpty(model.ReferenceRsi) && layer.Rsi != null &&
        CMU3DSourceReference.Matches(layer.Rsi, model.ReferenceRsi) &&
        layer.IdentityTransform && layer.Color == Color.White;
}

public readonly record struct CMU3DButtonLayer(string? Rsi, string? State, int Frame, bool Visible,
    Color Color, bool IdentityTransform);
