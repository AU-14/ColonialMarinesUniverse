using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVascularTearComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUEmbeddedForeignBodyComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCompartmentPressureComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUContaminatedWoundComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBoneSplinteredComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUOrganAdhesionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUOrganHemorrhageComponent : Component;

[ByRefEvent]
public readonly record struct CMUSurgicalTraitChangedEvent(
    EntityUid Body,
    EntityUid Part,
    CMUSurgicalTrait Trait,
    bool Removed);
