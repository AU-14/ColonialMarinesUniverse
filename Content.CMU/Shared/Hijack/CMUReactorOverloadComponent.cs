using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Hijack;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUReactorOverloadComponent : Component
{
    [DataField, AutoNetworkedField] public bool Overloaded;
}

[Serializable, NetSerializable]
public sealed partial class CMUReactorOverloadDoAfterEvent : SimpleDoAfterEvent;
