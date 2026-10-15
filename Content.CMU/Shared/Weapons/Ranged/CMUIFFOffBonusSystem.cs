using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Ranged;

/// <summary>
///     Raised on a gun after its IFF toggle is switched.
/// </summary>
[ByRefEvent]
public readonly record struct CMUGunIFFToggledEvent(bool Enabled);

/// <summary>
///     A gun that hits a little harder and holds its damage over range a little better with its IFF switched off.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUIFFOffBonusComponent : Component
{
    /// <summary>Multiplies the gun's damage while IFF is off.</summary>
    [DataField]
    public FixedPoint2 DamageMultiplier = 1.05;

    /// <summary>Multiplies the gun's damage falloff while IFF is off; lower loses less damage over range.</summary>
    [DataField]
    public FixedPoint2 FalloffMultiplier = 0.85;
}

public sealed class CMUIFFOffBonusSystem : EntitySystem
{
    [Dependency] private CMGunSystem _cmGun = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUIFFOffBonusComponent, CMUGunIFFToggledEvent>(OnToggled);
        SubscribeLocalEvent<CMUIFFOffBonusComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUIFFOffBonusComponent, GetGunDamageModifierEvent>(OnGetDamage);
        SubscribeLocalEvent<CMUIFFOffBonusComponent, GetDamageFalloffEvent>(OnGetFalloff);
    }

    private void OnMapInit(Entity<CMUIFFOffBonusComponent> ent, ref MapInitEvent args)
    {
        Refresh(ent);
    }

    private void OnToggled(Entity<CMUIFFOffBonusComponent> ent, ref CMUGunIFFToggledEvent args)
    {
        Refresh(ent);
    }

    private void Refresh(EntityUid gun)
    {
        _cmGun.RefreshGunDamageMultiplier(gun);
        _gun.RefreshModifiers(gun);
    }

    private bool IffOff(EntityUid gun)
    {
        return TryComp(gun, out IFFToggleComponent? toggle) && !toggle.Enabled;
    }

    private void OnGetDamage(Entity<CMUIFFOffBonusComponent> ent, ref GetGunDamageModifierEvent args)
    {
        if (IffOff(ent))
            args.Multiplier *= ent.Comp.DamageMultiplier;
    }

    private void OnGetFalloff(Entity<CMUIFFOffBonusComponent> ent, ref GetDamageFalloffEvent args)
    {
        if (IffOff(ent))
            args.FalloffMultiplier *= ent.Comp.FalloffMultiplier;
    }
}
