using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Medical.Scanner;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HealthScannerComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound;

    [DataField]
    public EntityUid? Target;

    // CMU14: yautja analyzers are tuned for alien physiology and scan xeno targets
    [DataField]
    public bool AllowXenoTargets;
}
