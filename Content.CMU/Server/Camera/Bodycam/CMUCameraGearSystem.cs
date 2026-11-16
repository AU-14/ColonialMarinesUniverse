using Content.Server.Camera;
using Content.Shared._RMC14.Camera;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.BadgeNumber;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server.CMU14.Camera.Bodycam;

/// <summary>
/// Puts worn <see cref="CMUCameraGearComponent"/> gear on its camera network, listed by the wearer's surname and
/// badge number, and takes it off again when it's removed or switched off. The listing is worked out when the gear goes on and
/// again when the wearer is issued a badge, never on a timer.
/// </summary>
public sealed class CMUCameraGearSystem : EntitySystem
{
    [Dependency] private SharedRMCCameraSystem _camera = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private CameraNetworkSystem _networks = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUCameraGearComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CMUCameraGearComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<CMUCameraGearComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUCameraGearComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnEquipped(Entity<CMUCameraGearComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        ent.Comp.Wearer = args.EquipTarget;
        UpdateName(ent);
        if (_toggle.IsActivated(ent.Owner))
            _networks.AddNetwork(ent, ent.Comp.Network);
    }

    private void OnUnequipped(Entity<CMUCameraGearComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Wearer != args.EquipTarget)
            return;

        ent.Comp.Wearer = null;
        ent.Comp.ShownName = null;
        _networks.RemoveNetwork(ent, ent.Comp.Network);
        _camera.SetCameraName(ent, Name(ent), null);
    }

    // Switched on or off: only worn gear is on the network, so taking it off the wearer already handles the rest.
    private void OnToggled(Entity<CMUCameraGearComponent> ent, ref ItemToggledEvent args)
    {
        if (ent.Comp.Wearer == null)
            return;

        if (args.Activated)
        {
            _networks.AddNetwork(ent, ent.Comp.Network);
            _networks.RefreshMemberDirectory(ent);
        }
        else
        {
            _networks.RemoveNetwork(ent, ent.Comp.Network);
        }
    }

    private void OnExamined(Entity<CMUCameraGearComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_toggle.IsActivated(ent.Owner))
        {
            args.PushMarkup(Loc.GetString("cmu-camera-gear-examine-switched-off"));
            return;
        }

        args.PushMarkup(ent.Comp.Wearer != null && ent.Comp.ShownName is { } name
            ? Loc.GetString("cmu-camera-gear-examine-live", ("name", name))
            : Loc.GetString("cmu-camera-gear-examine-off"));
    }

    /// <summary>
    /// Lists the gear as "Surname #1234", from the wearer's ID if they carry one.
    /// </summary>
    private void UpdateName(Entity<CMUCameraGearComponent> ent)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return;

        var fullName = Name(wearer);
        string? badge = null;
        if (_idCard.TryFindIdCard(wearer, out var card))
        {
            if (!string.IsNullOrWhiteSpace(card.Comp.FullName))
                fullName = card.Comp.FullName;

            if (TryComp(card, out CMUBadgeNumberComponent? number))
                badge = CMUBadgeNumberSystem.Format(number.Number);
        }

        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var surname = words.Length > 0 ? words[^1] : fullName;
        var shown = badge == null
            ? surname
            : Loc.GetString("cmu-camera-gear-name", ("surname", surname), ("badge", badge));

        if (shown == ent.Comp.ShownName)
            return;

        ent.Comp.ShownName = shown;
        _camera.SetCameraName(ent, shown, null);
        _networks.RefreshMemberDirectory(ent);
    }

    /// <summary>
    /// Re-lists any camera gear the wearer has on, after their ID or badge number changes.
    /// </summary>
    public void RefreshWornBy(EntityUid wearer)
    {
        // Every slot: each gear sets its own, and Wearer is only set when it's in one of them.
        var slots = _inventory.GetSlotEnumerator(wearer);
        while (slots.NextItem(out var item))
        {
            if (TryComp(item, out CMUCameraGearComponent? gear) && gear.Wearer == wearer)
                UpdateName((item, gear));
        }
    }
}
