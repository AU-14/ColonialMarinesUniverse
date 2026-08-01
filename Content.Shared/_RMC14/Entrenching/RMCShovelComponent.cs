using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Entrenching;

/// <summary>
///     Put on an entity alongside <see cref="EntrenchingToolComponent"/> to make all of its digging, filling,
///     dirt mound and HESCO building actions faster by dividing their delay by <see cref="SpeedMultiplier"/>
///     (e.g. 1.1 = 10% faster). Unlike the entrenching tool, a shovel with this component does not need an
///     <c>ItemToggle</c> to be folded/unfolded before use - just don't give it one.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(BarricadeSystem))]
public sealed partial class RMCShovelComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.1f;
}
