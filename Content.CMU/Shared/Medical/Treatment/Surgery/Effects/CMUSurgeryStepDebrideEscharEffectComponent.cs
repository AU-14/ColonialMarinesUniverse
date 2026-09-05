using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery.Effects;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepDebrideEscharEffectComponent : Component
{
}
