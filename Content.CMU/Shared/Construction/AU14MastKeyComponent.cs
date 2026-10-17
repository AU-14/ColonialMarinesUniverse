using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Construction;

/// <summary>
///     The COMSEC keys loaded into a field mast's feed. A mast only relays the nets of the factions keyed into
///     it, so raising one is not enough - somebody has to load a fill card before the feed is sealed. Keys are
///     loaded and wiped only while the feed is open (the <see cref="FeedOpenNode"/> graph node), which is also
///     the only state in which the mast relays nothing. Rides along on the untuned and finished mast entities;
///     the key set is carried across the construction entity swap between them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14MastKeyComponent : Component
{
    /// <summary>Faction ids (as written on the fill card) whose nets this mast relays.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Keys = new();

    /// <summary>Nets each keyable faction's card opens. A card for a faction missing here is refused.</summary>
    [DataField(required: true)]
    public Dictionary<string, List<ProtoId<RadioChannelPrototype>>> FactionChannels = new();

    [DataField]
    public string FeedOpenNode = "feedOpen";

    [DataField]
    public TimeSpan KeyDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan ZeroizeDelay = TimeSpan.FromSeconds(6);
}
