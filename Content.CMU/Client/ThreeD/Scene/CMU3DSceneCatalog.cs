using System.Linq;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Doors.Components;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Exact references win; first-parent lineage only supplies explicit review candidates.</summary>
public sealed partial class CMU3DSceneCatalog
{
    public float TerrainQueryPadding { get; private set; }
    private readonly Dictionary<string, CMU3DModelPrototype> _references = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CMU3DSceneMatch?> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CMU3DModelPrototype> _models = new(StringComparer.Ordinal);
    private readonly Func<string, string[]?> _parents;
    private readonly HashSet<string> _randomPrototypes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Prototype, string Layer, string State), CMU3DModelPrototype> _randomReferences = [];
    private readonly HashSet<(string Prototype, string Layer, string State)> _ambiguousRandomReferences = [];

    public CMU3DSceneCatalog(IEnumerable<CMU3DModelPrototype> models, Func<string, string[]?> parents)
    {
        _parents = parents;
        foreach (var model in models.OrderBy(model => model.Status != "reviewed").ThenBy(model => model.ID, StringComparer.Ordinal))
        {
            _models.TryAdd(model.ID, model);
            foreach (var reference in model.SourcePrototypes)
                _references.TryAdd(reference, model);
            foreach (var reference in model.RandomSpritePrototypes)
            {
                _randomPrototypes.Add(reference);
                if (string.IsNullOrEmpty(model.RandomSpriteLayer) || string.IsNullOrEmpty(model.ReferenceState) ||
                    string.IsNullOrEmpty(model.ReferenceRsi))
                    continue;
                var key = (reference, model.RandomSpriteLayer, model.ReferenceState);
                if (!_randomReferences.TryAdd(key, model))
                    _ambiguousRandomReferences.Add(key);
            }
        }
        foreach (var model in _models.Values)
        {
            if (!CMU3DTerrainCutout.Valid(model))
                continue;
            var low = model.TerrainCutoutMin;
            var high = model.TerrainCutoutMax;
            var reach = new System.Numerics.Vector2(MathF.Max(MathF.Abs(low.X), MathF.Abs(high.X)),
                MathF.Max(MathF.Abs(low.Y), MathF.Abs(high.Y))).Length() + model.GroundOffset.Length();
            foreach (var target in model.TerrainCutoutTargets)
            {
                if (!_references.TryGetValue(target.Id, out var terrain))
                    continue;
                var terrainReach = 0f;
                foreach (var part in terrain.Parts)
                {
                    part.Bounds(out var min, out var max);
                    terrainReach = MathF.Max(terrainReach, new System.Numerics.Vector2(
                        MathF.Max(MathF.Abs(min.X), MathF.Abs(max.X)), MathF.Max(MathF.Abs(min.Y), MathF.Abs(max.Y))).Length());
                }
                TerrainQueryPadding = MathF.Max(TerrainQueryPadding, reach + terrainReach + terrain.GroundOffset.Length());
            }
        }
    }

    public bool HasRandomSpriteVariants(string prototype) => _randomPrototypes.Contains(prototype);

    /// <summary>Only a known, single white source layer can use these static assemblies; no default-state guess or ancestor fallback.</summary>
    public CMU3DSceneMatch? ResolveRandomSprite(string prototype,
        IReadOnlyDictionary<string, (string State, Color? Color)> selected)
    {
        if (selected.Count != 1)
            return null;
        foreach (var (layer, choice) in selected)
        {
            if (choice.Color is { } color && color != Color.White)
                return null;
            var key = (prototype, layer, choice.State);
            if (!_ambiguousRandomReferences.Contains(key) && _randomReferences.TryGetValue(key, out var model))
                return new CMU3DSceneMatch(model, true, prototype);
        }
        return null;
    }

    /// <summary>Keep authored frame families for source-layer selection; other doors require explicit stable poses.</summary>
    public CMU3DSceneMatch? WithDoorState(CMU3DSceneMatch? match, DoorState state)
    {
        if (match is { } animated && animated.Model.DoorSpriteStates.Count > 0 && CMU3DDoorAppearance.SupportedState(state))
            return animated;
        if (match is not { } value || state is not (DoorState.Closed or DoorState.Open))
            return null;
        if (value.Model.DoorState == state)
            return value;
        if (value.Model.AlternateDoorModel is { } alternate &&
            _models.TryGetValue(alternate.Id, out var model) && model.DoorState == state)
            return value with { Model = model };
        return null;
    }

    public CMU3DSceneMatch? WithFoldState(CMU3DSceneMatch? match, bool folded)
    {
        if (match is not { } value)
            return null;
        if (value.Model.Folded == folded)
            return value;
        if (value.Model.AlternateFoldModel is { } alternate &&
            _models.TryGetValue(alternate.Id, out var model) && model.Folded == folded &&
            model.AlternateFoldModel?.Id == value.Model.ID)
            return value with { Model = model };
        return null;
    }

    public CMU3DSceneMatch? Resolve(string prototype)
    {
        if (_cache.TryGetValue(prototype, out var cached))
            return cached;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var result = Resolve(prototype, prototype, visited);
        _cache[prototype] = result;
        return result;
    }

    /// <summary>Source direction is chosen from the entity angle, independently of the review camera.</summary>
    public CMU3DSceneMatch? WithDirection(CMU3DSceneMatch? match, float yaw)
    {
        if (match is not { } value || value.Model.DirectionalModels.Length == 0)
            return match;
        var source = value.Model;
        if (!float.IsFinite(yaw) || source.SourceDirections is not (4 or 8) ||
            source.DirectionalModels.Length != source.SourceDirections)
            return null;
        var turn = (int) MathF.Floor((yaw % MathF.Tau) / (MathF.Tau / source.SourceDirections) + .5f);
        turn = (turn % source.SourceDirections + source.SourceDirections) % source.SourceDirections;
        var index = source.SourceDirections == 4
            ? turn switch { 1 => 2, 2 => 1, 3 => 3, _ => 0 }
            : turn switch { 1 => 4, 2 => 2, 3 => 6, 4 => 1, 5 => 7, 6 => 3, 7 => 5, _ => 0 };
        if (string.IsNullOrEmpty(source.DirectionalModels[index]) ||
            !_models.TryGetValue(source.DirectionalModels[index], out var target) ||
            target.ReferenceDirection != index || target.SourceDirections != source.SourceDirections ||
            target.ReferenceRsi != source.ReferenceRsi || target.ReferenceState != source.ReferenceState ||
            target.SourceSpriteRotates != source.SourceSpriteRotates ||
            !target.DirectionalModels.SequenceEqual(source.DirectionalModels))
            return null;
        return value with { Model = target };
    }

    private CMU3DSceneMatch? Resolve(string original, string current, HashSet<string> visited)
    {
        if (!visited.Add(current))
            return null;
        if (_references.TryGetValue(current, out var model))
            return new CMU3DSceneMatch(model, current == original, current);
        if (_parents(current) is not { } parents)
            return null;
        foreach (var parent in parents)
        {
            if (Resolve(original, parent, visited) is { } match)
                return match;
        }
        return null;
    }
}

public readonly record struct CMU3DSceneMatch(CMU3DModelPrototype Model, bool Exact, string Reference);
