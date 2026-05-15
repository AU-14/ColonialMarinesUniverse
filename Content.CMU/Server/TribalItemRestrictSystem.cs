using Content.Shared.AU14;
using Content.Shared.Item;
using Content.Shared.Tag;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14;

public sealed class TribalItemRestrictSystem : EntitySystem
{
	[Dependency] private readonly TagSystem _tagSystem = default!;
	[Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    private static readonly ProtoId<TagPrototype> tribaltag = "tribe";


	public override void Initialize()
	{
		base.Initialize();
		// Prevent non-tribal entities from picking up tribal-restricted items
		SubscribeLocalEvent<ItemComponent, PickupAttemptEvent>(OnItemPickupAttempt);
		SubscribeLocalEvent<ItemComponent, GettingPickedUpAttemptEvent>(OnItemGettingPickedUpAttempt);
		SubscribeLocalEvent<ItemComponent, BeingEquippedAttemptEvent>(OnItemEquipAttempt);
	}

	private bool IsItemTribalRestricted(EntityUid item)
	{
		// Check if the item has a "tribe" tag
		if (!TryComp(item, out TagComponent? tags))
			return false;

		return _tagSystem.HasTag(item, tribaltag);
	}

	private void OnItemPickupAttempt(Entity<ItemComponent> item, ref PickupAttemptEvent args)
	{
		if (args.Cancelled)
			return;

		// If the item doesn't have tribe tag, allow pickup
		if (!IsItemTribalRestricted(item))
			return;

		// If the picker has TribalComponent, allow pickup
		if (HasComp<TribalComponent>(args.User))
			return;

		// Block non-tribals from picking up tribe-tagged items
		args.Cancel();
		_popupSystem.PopupClient("You cannot pick this up.", item, args.User);
	}

	private void OnItemGettingPickedUpAttempt(Entity<ItemComponent> item, ref GettingPickedUpAttemptEvent args)
	{
		if (args.Cancelled)
			return;

		// If the item doesn't have tribe tag, allow pickup
		if (!IsItemTribalRestricted(item))
			return;

		// If the picker has TribalComponent, allow pickup
		if (HasComp<TribalComponent>(args.User))
			return;

		// Block non-tribals from picking up tribe-tagged items
		args.Cancel();
		_popupSystem.PopupClient("You cannot pick this up.", item, args.User);
	}

	private void OnItemEquipAttempt(Entity<ItemComponent> item, ref BeingEquippedAttemptEvent args)
	{
		if (args.Cancelled)
			return;

		// If the item doesn't have tribe tag, allow equip
		if (!IsItemTribalRestricted(item))
			return;

		// If the equipee has TribalComponent, allow equip
		if (HasComp<TribalComponent>(args.Equipee))
			return;

		// Block non-tribals from equipping tribe-tagged items
		args.Cancel();
		_popupSystem.PopupClient("You cannot equip this.", item, args.Equipee);
	}
}
