using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ShipWeapons;

// CMU14 BadBlood: hooks that let game rules offer their own entries on the overwatch fire-mission picker
// (docs/GraphAudit-BadBlood-ObjectivesPlan.md ARCH-505). With no subscriber the picker behaves exactly as before.

/// <summary>
/// Raised on an overwatch console whenever the server rebuilds its fire-mission list. Subscribers append entries to
/// <paramref name="Extras"/>; they are listed after the ship's armed weapons and are never picked automatically.
/// An extra with <see cref="CMUShipWeaponStatus.Remaining"/> 0 shows but cannot be fired.
/// </summary>
[ByRefEvent]
public readonly record struct CMUShipWeaponsCollectExtrasEvent(EntityUid Ship, List<CMUShipWeaponStatus> Extras);

/// <summary>
/// Raised on an overwatch console when a launch passed the access check, before any fire-mission check.
/// A subscriber that owns <paramref name="Weapon"/> sets <see cref="Handled"/> and the normal launch is skipped.
/// </summary>
[ByRefEvent]
public record struct CMUShipWeaponLaunchInterceptEvent(
    EntityUid User,
    ProtoId<CMUShipWeaponPrototype>? Weapon,
    bool Handled = false);
