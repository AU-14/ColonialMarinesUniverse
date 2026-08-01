// Content.Shared/AU14/ColonyEconomy/SubmissionStorageComponent.cs
using Robust.Shared.GameStates;

namespace Content.Shared.AU14.ColonyEconomy;

[RegisterComponent, NetworkedComponent]
public sealed partial class ColonyAtmComponent : Component
{
    /// <summary>
    ///     The ID card entity that was swiped on this ATM.
    ///     Set when a player uses an ID card on the machine, cleared when the UI closes.
    /// </summary>
    public EntityUid? SwipedCard;
}
