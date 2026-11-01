using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Samples source-owned layers. No charge calculation, insertion simulation or animation timer.</summary>
public static class CMU3DChargerAppearance
{
    public const string Rsi = "_RMC14/Structures/Power/recharger.rsi";
    public const string Taser = "recharger-taser";
    public const string Baton = "recharger-baton";

    public static bool TryParts(CMU3DModelPrototype model, CMU3DChargerLayer basis,
        CMU3DChargerLayer light, CMU3DChargerLayer? taser, CMU3DChargerLayer? baton,
        bool extraVisibleLayer, out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (extraVisibleLayer || model.ChargerAppearance is not { } definition || model.ReferenceRsi != Rsi ||
            model.ReferenceState != "recharger" || model.SourceDirections != 1 || model.BakedSpriteTint != Color.White ||
            model.UseEntityRotation || model.Placement != "surface" || model.SpriteStates.Count != 0 ||
            !basis.Visible || basis.State != "recharger" || !Valid(basis, false, 1) ||
            definition.BaseParts.Count == 0 || definition.LightStates.Count != 6 || definition.InsertedStates.Count != 2 ||
            !definition.InsertedStates.ContainsKey(Taser) || !definition.InsertedStates.ContainsKey(Baton) ||
            light.State == null || !definition.LightStates.TryGetValue(light.State, out var state))
            return false;
        var expectedFrames = light.State == "recharger-5" ? 2 : 1;
        if (light.State is not ("recharger-0" or "recharger-1" or "recharger-2" or "recharger-3" or "recharger-4" or "recharger-5") ||
            !Valid(light, true, expectedFrames) || state.Frames.Count != expectedFrames || state.Delays.Count != expectedFrames ||
            !ValidInsert(taser, Taser) || !ValidInsert(baton, Baton))
            return false;
        foreach (var delay in state.Delays)
            if (!float.IsFinite(delay) || MathF.Abs(delay - (expectedFrames == 2 ? .1f : 1f)) > .000001f)
                return false;
        List<CMU3DModelPart> result = [];
        result.AddRange(definition.BaseParts);
        if (light.Visible)
            result.AddRange(state.Frames[light.Frame].Parts);
        if (taser is { Visible: true })
            result.AddRange(definition.InsertedStates[Taser].Parts);
        if (baton is { Visible: true })
            result.AddRange(definition.InsertedStates[Baton].Parts);
        if (result.Count is < 1 or > 128)
            return false;
        foreach (var part in result)
            if (!part.Valid || !CMU3DReagentTankAppearance.ValidColor(part.Color))
                return false;
        var mask = (taser is { Visible: true } ? 1 : 0) | (baton is { Visible: true } ? 2 : 0);
        key = $"charger:{(light.Visible ? light.State : "hidden")}:{light.Frame}:{mask}";
        parts = result;
        return true;
    }

    private static bool ValidInsert(CMU3DChargerLayer? layer, string state) =>
        layer == null || layer.Value.State == state && Valid(layer.Value, false, 1);

    private static bool Valid(CMU3DChargerLayer layer, bool unshaded, int frames) =>
        layer.Rsi?.TrimStart('/') == "Textures/" + Rsi && layer.IdentityTransform &&
        layer.Color == Color.White && layer.Unshaded == unshaded && layer.FrameCount == frames &&
        layer.Frame >= 0 && layer.Frame < frames;
}

public readonly record struct CMU3DChargerLayer(string? Rsi, string? State, int Frame, int FrameCount,
    bool Visible, Color Color, bool IdentityTransform, bool Unshaded);
