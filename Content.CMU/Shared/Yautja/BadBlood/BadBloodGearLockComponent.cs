using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// ECS-01: Bad Blood gear refuses anyone who is not a Yautja. They may pick it up (to loot it, carry it off or
/// destroy it) but cannot wear it, swing it, throw it, fire it or use it in hand. Stricter than
/// <see cref="YautjaTechItemComponent"/>, which only stops firing and in-hand use, and without its tracking side effects.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodGearLockComponent : Component;
