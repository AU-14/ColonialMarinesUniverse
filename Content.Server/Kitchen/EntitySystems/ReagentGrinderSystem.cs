using Content.Shared.Kitchen.EntitySystems;

namespace Content.Server.Kitchen.EntitySystems
{
    [UsedImplicitly]
    internal sealed partial class ReagentGrinderSystem : EntitySystem
    {
        [Dependency] private IGameTiming _timing = default!;
        [Dependency] private SharedSolutionContainerSystem _solutionContainersSystem = default!;
        [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private SharedPopupSystem _popupSystem = default!;
        [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private StackSystem _stackSystem = default!;
        [Dependency] private SharedAudioSystem _audioSystem = default!;
        [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
        [Dependency] private SharedContainerSystem _containerSystem = default!;
        [Dependency] private SharedDestructibleSystem _destructible = default!;
        [Dependency] private RandomHelperSystem _randomHelper = default!;
        [Dependency] private JitteringSystem _jitter = default!;

/// <inheritdoc />
public sealed class ReagentGrinderSystem : SharedReagentGrinderSystem;
