namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// Marks a map entity as an AUTHORED level: one loaded from a <c>GameMapPrototype</c>'s <c>mapPath</c>,
/// <c>mapsAbove</c> or <c>mapsBelow</c> by <c>CMUZLevelsSystem.OnGameMapLoad</c>. Generated stone placed on such a
/// level is localized to its void tiles, so mapped terrain never joins the cave-in graph.
///
/// Only the game-map load adds it. Runtime levels (<c>CreateStoneBelow</c>, <c>EnsureNeighborLevel</c>, MultiDeck,
/// expeditions, the mapping and combine commands) must never get it. It is unsaved, so the mapping command never
/// writes it into a map file.
/// </summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class ZAuthoredLevelComponent : Component;
