using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Dropship.MultiDeck;

/// <summary>
/// A single flight controller with additional grids on relative Z levels.
/// The primary grid owns FTL, destinations and equipment; decks never fly independently.
/// </summary>
[RegisterComponent]
public sealed partial class MultiDeckDropshipComponent : Component
{
    [DataField(required: true)]
    public Dictionary<int, ResPath> DeckPaths = new();

    /// <summary>Height of the primary deck above a landing marker.</summary>
    [DataField]
    public int LandingOffset = 1;

    /// <summary>Servicing/undercarriage grids that must leave loose ground occupants behind on departure.</summary>
    [DataField]
    public HashSet<int> ExteriorDecks = new();

    public readonly Dictionary<int, EntityUid> Decks = new();
    public bool Initialized;
    public bool Synchronizing;
}

/// <summary>Resolves entities on a secondary deck to the ship's flight controller.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class DropshipDeckComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Ship;

    [DataField, AutoNetworkedField]
    public int Offset;
}
