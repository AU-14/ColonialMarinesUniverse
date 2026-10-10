using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats.Mobs.Biomorph;

public sealed partial class BiomorphAssimilateActionEvent : EntityTargetActionEvent;
public sealed partial class BiomorphPlantKudzuActionEvent : InstantActionEvent;
public sealed partial class BiomorphMimicTransformActionEvent : InstantActionEvent;
public sealed partial class BiomorphMimicDrawVenomActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class BiomorphAssimilateDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Sent from the mimic's profile-picker BUI to the server when the player selects a form.
/// </summary>
[Serializable, NetSerializable]
public sealed class BiomorphMimicSelectFormMessage : BoundUserInterfaceMessage
{
    public BiomorphMimicSelectFormMessage(int index) => Index = index;
    public int Index { get; }
}

/// <summary>
///     State pushed to clients so the picker can render the current pool of forms.
/// </summary>
[Serializable, NetSerializable]
public sealed class BiomorphMimicBuiState : BoundUserInterfaceState
{
    public BiomorphMimicBuiState(List<string> profileNames, int? activeIndex)
    {
        ProfileNames = profileNames;
        ActiveIndex = activeIndex;
    }

    public List<string> ProfileNames { get; }
    public int? ActiveIndex { get; }
}

[Serializable, NetSerializable]
public enum BiomorphMimicUiKey : byte
{
    Key
}
