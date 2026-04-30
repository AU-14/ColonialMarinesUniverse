using System.Collections.Generic;
using Content.Shared._RMC14.Medical.Wounds;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Wounds;

/// <summary>
///     Per-body-part wound ledger. Each entry mirrors into RMC's entity-level
///     <see cref="WoundedComponent"/> on the body owner so the existing
///     health analyzer / holocard / bandage pipelines keep working unchanged.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCMUWoundsSystem))]
public sealed partial class BodyPartWoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<Wound> Wounds = new();

    /// <summary>
    ///     Kept in lockstep with <see cref="Wounds"/>. A shorter list is
    ///     tolerated for save-game forward-compat — readers treat missing
    ///     entries as <see cref="WoundSize.Deep"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<WoundSize> Sizes = new();

    /// <summary>
    ///     Number of bandages applied to each wound. The wound becomes
    ///     <c>Treated</c> once this reaches the size profile requirement.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<int> Bandages = new();

    [DataField, AutoPausedField]
    public TimeSpan NextHealTick;
}
