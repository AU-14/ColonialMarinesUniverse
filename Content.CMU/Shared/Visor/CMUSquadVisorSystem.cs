using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared._RMC14.Visor;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Visor;

/// <summary>
/// The squad optic, while switched down, gives its wearer the squad leader / fireteam leader tracker if they don't
/// already have one, lets them see friendly primed grenades and gives their guns IFF.
/// </summary>
public sealed partial class CMUSquadVisorSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AttachableIFFSystem _attachableIff = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMGunSystem _rmcGun = default!;
    [Dependency] private SquadLeaderTrackerSystem _tracker = default!;
    [Dependency] private VisorSystem _visor = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSquadVisorActiveComponent, ComponentStartup>(OnActiveStartup);
        SubscribeLocalEvent<CMUSquadVisorActiveComponent, ComponentShutdown>(OnActiveShutdown);
        SubscribeLocalEvent<CMUSquadVisorActiveComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CMUSquadVisorActiveComponent, GotUnequippedEvent>(OnUnequipped);

        InitializeIFF();
        InitializeSmartgun();
    }

    private void OnActiveStartup(Entity<CMUSquadVisorActiveComponent> helmet, ref ComponentStartup args)
    {
        if (TryGetWearer(helmet, out var wearer))
            Apply(wearer);
    }

    private void OnActiveShutdown(Entity<CMUSquadVisorActiveComponent> helmet, ref ComponentShutdown args)
    {
        if (TryGetWearer(helmet, out var wearer))
            Remove(wearer, helmet);
    }

    private void OnEquipped(Entity<CMUSquadVisorActiveComponent> helmet, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.HEAD) != 0)
            Apply(args.EquipTarget);
    }

    private void OnUnequipped(Entity<CMUSquadVisorActiveComponent> helmet, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.HEAD) != 0)
            Remove(args.EquipTarget, helmet);
    }

    private bool TryGetWearer(EntityUid helmet, out EntityUid wearer)
    {
        wearer = default;
        if (!_inventory.TryGetContainingSlot(helmet, out var slot) || (slot.SlotFlags & SlotFlags.HEAD) == 0)
            return false;

        wearer = Transform(helmet).ParentUid;
        return wearer.IsValid();
    }

    private void Apply(EntityUid wearer)
    {
        if (_timing.ApplyingState || HasComp<CMUSquadVisorWearerComponent>(wearer))
            return;

        var marker = AddComp<CMUSquadVisorWearerComponent>(wearer);
        marker.GrantedTracker = _tracker.CMUTryGrantSquadVisorTracker(wearer);
        Dirty(wearer, marker);

        AddToggleActions(wearer, marker);
        UpdateHeldGuns(wearer);
    }

    private void Remove(EntityUid wearer, EntityUid helmet)
    {
        if (_timing.ApplyingState || !TryComp<CMUSquadVisorWearerComponent>(wearer, out var marker))
            return;

        if (marker.GrantedTracker)
            _tracker.CMURemoveSquadVisorTracker(wearer);

        RemoveToggleActions(wearer, marker);
        RemComp<CMUSquadVisorWearerComponent>(wearer);
        UpdateHeldGuns(wearer);
    }
}
