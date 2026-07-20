using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server.Nutrition.EntitySystems
{
    [UsedImplicitly]
    public sealed partial class CreamPieSystem : SharedCreamPieSystem
    {
        [Dependency] private SharedSolutionContainerSystem _solutions = default!;
        [Dependency] private PuddleSystem _puddle = default!;
        [Dependency] private ItemSlotsSystem _itemSlots = default!;
        [Dependency] private TriggerSystem _trigger = default!;
        [Dependency] private SharedAudioSystem _audio = default!;
        [Dependency] private PopupSystem _popup = default!;

public sealed class CreamPieSystem : SharedCreamPieSystem;
