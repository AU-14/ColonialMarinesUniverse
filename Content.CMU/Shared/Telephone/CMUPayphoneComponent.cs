using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Telephone;

/// <summary>
/// A public payphone. Named after the area it stands in so each one is identifiable in the phone list.
/// Each call costs <see cref="CallCost"/>, paid from cash in hand or else the caller's ID card account. 911 is free.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUPayphoneComponent : Component
{
    [DataField]
    public int CallCost = 2;

    /// <summary>Directory sections that cost something else to call, like the long-distance comms relay.</summary>
    [DataField]
    public Dictionary<string, int> CategoryCosts = new() { ["Comms Relay"] = 25 };

    /// <summary>What calling a phone in <paramref name="category"/> costs.</summary>
    public int CostFor(string category)
    {
        return CategoryCosts.TryGetValue(category, out var cost) ? cost : CallCost;
    }
}

/// <summary>
/// Raised on a phone just before it places a call to <see cref="Target"/>; cancel it to stop the call.
/// </summary>
[ByRefEvent]
public record struct CMUPhoneCallAttemptEvent(EntityUid User, EntityUid Target)
{
    public bool Cancelled;
}
