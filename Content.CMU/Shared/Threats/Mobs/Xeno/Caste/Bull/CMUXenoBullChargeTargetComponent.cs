using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Bull;

[RegisterComponent, NetworkedComponent, Access(typeof(CMUXenoBullChargeSystem))]
public sealed partial class CMUXenoBullChargeTargetComponent : Component;
