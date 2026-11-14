namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The ghost-role entry of one reinforcement call. It stands at the Lodge while the call's seats are open and is
/// deleted when the squad arrives. A player who wins the raffle holds a seat, the body only exists on arrival.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodSquadSeatsComponent : Component
{
    [ViewVariables]
    public BadBloodSquadCall? Call;
}

/// <summary>
/// A Lodge part the clan calls reinforcement squads from. Sits on the Ascension Stone.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodSquadCallerComponent : Component;
