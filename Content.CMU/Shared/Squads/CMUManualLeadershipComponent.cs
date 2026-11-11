using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Squads;

/// <summary>
/// On a squad whose squad leader and fireteam leaders are only chosen by hand: nobody is promoted
/// automatically when a leader is missing, and people joining do not take over or get handed a
/// fireteam's leadership. Members are still sorted into fireteams. Toggled from overwatch.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUManualLeadershipComponent : Component;
