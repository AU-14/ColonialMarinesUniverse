using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Uses the damage and wire visualizers' layers, including repair and cutting. Owns no gameplay state.</summary>
public static class CMU3DBarricadeAppearance
{
    // Composed effects use the instanced scene renderer, rather than the static model BSP's budget.
    public const int AcidPartLimit = 160;

    public static bool ValidLayerOrder(int body, int reinforcement, int? acid, int? wire) =>
        body >= 0 && reinforcement > body && (acid == null || acid > reinforcement) &&
        (wire == null || wire > reinforcement && (acid == null || wire > acid));

    public static bool TryParts(CMU3DModelPrototype model, CMU3DBarricadeLayer body,
        CMU3DBarricadeLayer reinforcement, bool extraVisibleLayer, out IReadOnlyList<CMU3DModelPart> parts, out string key,
        CMU3DBarricadeLayer? wire = null, CMU3DBarricadeLayer? acid = null)
    {
        parts = [];
        key = string.Empty;
        if (extraVisibleLayer || model.SourceDirections != 4 || model.ReferenceState != "DamageOverlay_0" ||
            !Valid(body, model.ReferenceRsi) || !Valid(reinforcement, model.BarricadeReinforcementRsi))
            return false;

        var suffix = body.State switch
        {
            "DamageOverlay_0" => "0",
            "DamageOverlay_4" => "4",
            "DamageOverlay_8" => "8",
            "DamageOverlay_12" => "12",
            _ => null,
        };
        var wired = wire is { Visible: true };
        if (wired && (!Valid(wire!.Value, model.BarricadeWireRsi) ||
                      string.IsNullOrEmpty(model.BarricadeWireState) || wire.Value.State != model.BarricadeWireState))
            return false;
        var states = wired ? model.BarricadeWiredStates : model.BarricadeDamageStates;
        if (suffix == null || reinforcement.State != "AdditionalDamageOverlay_" + suffix ||
            !states.TryGetValue(suffix, out var frame) || frame.Parts.Count is < 1 or > 128)
            return false;
        var acided = acid is { Visible: true };
        if (acided)
        {
            var layer = acid!.Value;
            if (!Valid(layer, model.BarricadeAcidRsi, animated: true) ||
                string.IsNullOrEmpty(model.BarricadeAcidState) || layer.State != model.BarricadeAcidState ||
                model.BarricadeAcidDelays.Count != 5 ||
                !model.BarricadeAcidStates.TryGetValue(suffix, out var effect))
                return false;
            var frames = wired ? effect.WiredFrames : effect.Frames;
            if (frames.Count != 5 || layer.Frame is < 0 or >= 5)
                return false;
            frame = frames[layer.Frame];
            if (frame.Parts.Count is < 1 or > AcidPartLimit)
                return false;
        }
        foreach (var part in frame.Parts)
        {
            if (!part.Valid)
                return false;
        }
        parts = frame.Parts;
        key = "barricade-damage:" + suffix + (wired ? ":wire" : string.Empty) +
              (acided ? ":acid:" + acid!.Value.Frame : string.Empty);
        return true;
    }

    private static bool Valid(CMU3DBarricadeLayer layer, string? rsi, bool animated = false) =>
        !string.IsNullOrEmpty(rsi) && layer.Rsi != null &&
        CMU3DSourceReference.Matches(layer.Rsi, rsi) && layer.Visible &&
        (animated || layer.Frame == 0) && layer.Color == Color.White && layer.IdentityTransform;
}

public readonly record struct CMU3DBarricadeLayer(string? Rsi, string? State, int Frame, bool Visible,
    Color Color, bool IdentityTransform);
