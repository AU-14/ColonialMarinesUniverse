using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.CMU14.Backpack;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBackpackRocketAttachmentComponent : Component;

[RegisterComponent]
public sealed partial class CMUBackpackRocketLauncherComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMUBackpackRocketDetachDoAfterEvent : SimpleDoAfterEvent;
