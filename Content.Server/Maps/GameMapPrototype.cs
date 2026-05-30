using Content.Server.Station;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Diagnostics;
using System.Numerics;
using Content.Server.AU14;
using Content.Shared.AU14;

namespace Content.Server.Maps;

/// <summary>
/// Prototype data for a game map.
/// </summary>
/// <remarks>
/// Forks should not directly edit existing parts of this class.
/// Make a new partial for your fancy new feature, it'll save you time later.
/// </remarks>
[Prototype, PublicAPI]
[DebuggerDisplay("GameMapPrototype [{ID} - {MapName}]")]
public sealed partial class GameMapPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float MaxRandomOffset = 0;

    /// <summary>
    /// Turns out some of the map files are actually secretly grids. Excellent. I love map loading code.
    /// </summary>
    [DataField] public bool IsGrid;

    [DataField]
    public bool RandomRotation = false;

    /// <summary>
    /// Name of the map to use in generic messages, like the map vote.
    /// </summary>
    [DataField(required: true)]
    public string MapName { get; private set; } = default!;

    /// <summary>
    /// Relative directory path to the given map, i.e. `/Maps/saltern.yml`
    /// </summary>
    [DataField(required: true)]
    public ResPath MapPath { get; private set; } = default!;

    /// <summary>
    /// CrystallEdge: Additional maps loaded below the main map (at negative depth levels).
    /// Each map in the list is loaded at depth -N, -N+1, ..., -1, with <see cref="MapPath"/> at depth 0.
    /// </summary>
    [DataField]
    public List<ResPath> MapsBelow = new();

    /// <summary>
    /// CrystallEdge: additional maps loaded above the main map (at positive depth levels).
    /// Each map in the list is loaded at depth 1, 2, ..., N. <see cref="MapPath"/> works as depth 0.
    /// </summary>
    [DataField]
    public List<ResPath> MapsAbove = new();

    /// <summary>
    /// CrystallEdge: ability to setup shared components for all zLevels
    /// </summary>
    [DataField]
    public ComponentRegistry ZLevelsComponentOverrides = new();

    [DataField("stations", required: true)]
    private Dictionary<string, StationConfig> _stations = new();

    /// <summary>
    /// The stations this map contains. The names should match with the BecomesStation components.
    /// </summary>
    public IReadOnlyDictionary<string, StationConfig> Stations => _stations;

    /// <summary>
    /// Performs a shallow clone of this map prototype, replacing <c>MapPath</c> with the argument.
    /// </summary>
    public GameMapPrototype Persistence(ResPath mapPath)
    {
        var clone = (GameMapPrototype) MemberwiseClone();
        clone.MapPath = mapPath;
        return clone;
    }
}
