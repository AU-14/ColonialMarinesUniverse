using Content.Shared.CMU14.Chemistry.Fuel;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// FLOW-22: colour tier of a tile fire lit from a <see cref="CMUFireSpec"/>. The client tints the fire's sprite and
/// light with <see cref="Color"/> (<c>CMUFireTierVisualizerSystem</c>), so colour shows intensity rather than which
/// fire prototype was spawned. Set by <c>SharedRMCFlammableSystem.ApplyFireSpec</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUFireTierComponent : Component
{
    /// <summary>1 red, 2 orange, 3 yellow, 4 blue, 5 white; see <see cref="CMUFuelProfileSystem.TierFor"/>.</summary>
    [DataField, AutoNetworkedField]
    public int Tier = 1;

    [DataField, AutoNetworkedField]
    public Color Color = Color.Red;

    /// <summary>Firepenetrating level the fire was lit with; copied to fires it spreads to.</summary>
    [DataField]
    public int PenetrationLevel;

    /// <summary>Spread rule of the spec (see <see cref="CMUFireSpec.SpreadDepth"/>); copied to fires it spreads to.</summary>
    [DataField]
    public int SpreadDepth;
}

/// <summary>
/// Raised on a tile fire after a <see cref="CMUFireSpec"/> was applied to it, so server-only systems can act on the
/// spec (tile creep for Flowing fuels, suppression for Viscous ones). <paramref name="SpreadChild"/> is true when the
/// fire was spawned by another fire spreading, which already set the child's spread depth.
/// </summary>
[ByRefEvent]
public readonly record struct CMUFireSpecAppliedEvent(CMUFireSpec Spec, bool SpreadChild);
