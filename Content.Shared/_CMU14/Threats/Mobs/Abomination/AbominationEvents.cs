using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination;

public sealed partial class AbominationAssimilateActionEvent : EntityTargetActionEvent;
public sealed partial class AbominationPlantKudzuActionEvent : InstantActionEvent;
public sealed partial class AbominationMimicTransformActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class AbominationAssimilateDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Sent from the mimic's profile-picker BUI to the server when the player selects a form.
/// </summary>
[Serializable, NetSerializable]
public sealed class AbominationMimicSelectFormMessage : BoundUserInterfaceMessage
{
    public AbominationMimicSelectFormMessage(int index) => Index = index;
    public int Index { get; }
}

/// <summary>
///     State pushed to clients so the picker can render the current pool of forms.
/// </summary>
[Serializable, NetSerializable]
public sealed class AbominationMimicBuiState : BoundUserInterfaceState
{
    public AbominationMimicBuiState(List<string> profileNames, int? activeIndex)
    {
        ProfileNames = profileNames;
        ActiveIndex = activeIndex;
    }

    public List<string> ProfileNames { get; }
    public int? ActiveIndex { get; }
}

[Serializable, NetSerializable]
public enum AbominationMimicUiKey : byte
{
    Key
}
