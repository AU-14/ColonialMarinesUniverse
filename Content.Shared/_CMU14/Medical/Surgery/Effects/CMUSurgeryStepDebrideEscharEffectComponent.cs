using Content.Shared._CMU14.Medical.Surgery;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Effects;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepDebrideEscharEffectComponent : Component
{
}
