using Content.Shared.Alert;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.NightVision;

public readonly record struct NightVisionSettings(
    ProtoId<AlertPrototype>? Alert,
    NightVisionState State,
    bool Overlay,
    bool Innate,
    bool SeeThroughContainers,
    bool DrawVisibleMobs,
    bool Green,
    bool Mesons,
    bool ExperimentalMesonFov,
    bool BlockScopes,
    bool OnlyHalf,
    Color? Tint,
    float NoiseStrength,
    float VignetteStrength);
