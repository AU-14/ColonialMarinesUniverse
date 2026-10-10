using Content.Shared.Damage;

namespace Content.Server.CMU14.Weapons.Ranged.Hitscan;

/// <summary>
/// Lets a hitscan shot hit like an RMC projectile: its damage is applied with the shot as the tool, so
/// <c>CMArmorPiercing</c> counts, and <c>RMCAreaDamage</c> and <c>Electrified</c> on the shot apply to the target.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUHitscanImpactSystem))]
public sealed partial class CMUHitscanImpactComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;
}
