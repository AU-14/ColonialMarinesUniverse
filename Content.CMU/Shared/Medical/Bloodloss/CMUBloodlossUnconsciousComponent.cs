using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Bloodloss;

/// <summary>
/// Present while a mob's blood is too low to stay conscious. Holds them in critical, where they can still crawl,
/// and getting hit doesn't stop the crawl the way it does for a normal crit.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUBloodlossStateSystem))]
public sealed partial class CMUBloodlossUnconsciousComponent : Component;
