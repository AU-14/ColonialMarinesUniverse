using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class TachycardiaComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class ArrhythmiaComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class CardiacArrestComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class PulmonaryEdemaComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class HepaticFailureComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class RenalFailureComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class NauseaComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class TinnitusComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class DeafenedComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class ConcussedComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class TraumaticBrainInjuryComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class MissingArmComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class MissingLegComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class MissingHandComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class MissingFootComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class WhiplashComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class RecoveringFromSurgeryComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class TransplantRejectionComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUPainShockStatusComponent : Component { }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public BodyPartType Part = BodyPartType.Arm;
}
