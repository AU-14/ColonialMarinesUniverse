using Content.Shared._RMC14.Line;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedGunPredictionSystem), typeof(LineSystem))]
public sealed partial class IgnorePredictionHitComponent : Component;
