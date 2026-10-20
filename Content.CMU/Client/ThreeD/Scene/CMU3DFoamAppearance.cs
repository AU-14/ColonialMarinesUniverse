using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Reads the existing foam layers; never performs smoothing or starts another clock.</summary>
public static class CMU3DFoamAppearance
{
    public const string Prototype = "RMCFoamedAluminiumMetal";
    public const string Rsi = "Effects/foam.rsi";
    public const string BaseState = "metal_foam";
    public static readonly Color SourceTint = new(1f, 1f, 1f, .8f);
    public static readonly string[] Edges = ["south", "east", "north", "west"];
    public static readonly Vector2[] Offsets = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    public static bool TryParts(CMU3DModelPrototype model, IReadOnlyList<CMU3DFoamLayer> layers,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (model.FoamAppearance is not { } definition || layers.Count != 5 ||
            model.ReferencePrototype != Prototype || model.SourcePrototypes.Length != 1 ||
            model.SourcePrototypes[0] != Prototype || model.ReferenceRsi != Rsi ||
            model.ReferenceState != BaseState || model.SourceDirections != 1 ||
            model.BakedSpriteTint != SourceTint || model.ReferenceTint != SourceTint ||
            !model.UseEntityRotation || model.GroundOffset != Vector2.Zero || model.Placement != "floor" ||
            model.SpriteStates.Count != 0 || definition.BaseParts.Count == 0 || definition.EdgeParts.Count != 4)
            return false;
        var mask = 0;
        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (layer.Rsi?.TrimStart('/') != "Textures/" + Rsi ||
                layer.State != (i == 0 ? BaseState : BaseState + "-" + Edges[i - 1]) ||
                layer.Frame != 0 || layer.FrameCount != 1 || layer.Color != Color.White ||
                !layer.ValidTransform || layer.Offset != (i == 0 ? Vector2.Zero : Offsets[i - 1]) ||
                i == 0 && !layer.Visible)
                return false;
            if (i > 0 && layer.Visible)
                mask |= 1 << (i - 1);
        }
        List<CMU3DModelPart> result = [];
        result.AddRange(definition.BaseParts);
        for (var i = 0; i < Edges.Length; i++)
        {
            if (!definition.EdgeParts.TryGetValue(Edges[i], out var edge) || edge.Parts.Count == 0)
                return false;
            if ((mask & (1 << i)) != 0)
                result.AddRange(edge.Parts);
        }
        if (result.Count is < 1 or > 128)
            return false;
        foreach (var part in result)
            if (!part.Valid || part.Color != SourceTint)
                return false;
        parts = result;
        key = $"foam:edges-{mask}";
        return true;
    }
}

public readonly record struct CMU3DFoamLayer(string? Rsi, string? State, int Frame, int FrameCount,
    bool Visible, Color Color, Vector2 Offset, bool ValidTransform);
