using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Mutiny;

// command and MPs: can't be recruited and side with command automatically when a mutiny begins
// (cmss13 MUTINY_LOYALIST_ROLES)
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMutinyLoyalistComponent : Component;
