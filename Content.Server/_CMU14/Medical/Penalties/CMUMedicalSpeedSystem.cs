using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Penalties;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Penalties;

public sealed class CMUMedicalSpeedSystem : SharedCMUMedicalSpeedSystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnGunEquipped);
        SubscribeLocalEvent<CMUMedicalGunAimPenaltyComponent, GotUnequippedHandEvent>(OnGunUnequipped);
        SubscribeLocalEvent<CMUMedicalGunAimPenaltyComponent, HandSelectedEvent>(OnGunSelected);
    }

    protected override void RefreshAimDependentWeapons(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        var refreshed = new HashSet<EntityUid>();
        foreach (var held in _hands.EnumerateHeld((body, hands)))
        {
            if (!_gun.TryGetGun(held, out var gunUid, out GunComponent? gun))
                continue;

            if (!refreshed.Add(gunUid))
                continue;

            EnsureComp<CMUMedicalGunAimPenaltyComponent>(gunUid);
            _gun.RefreshModifiers((gunUid, gun));
        }
    }

    private void OnGunEquipped(Entity<GunComponent> gun, ref GotEquippedHandEvent args)
    {
        RefreshGunForUser(gun, args.User);
    }

    private void OnGunUnequipped(Entity<CMUMedicalGunAimPenaltyComponent> gun, ref GotUnequippedHandEvent args)
    {
        RemComp<CMUMedicalGunAimPenaltyComponent>(gun.Owner);
        _gun.RefreshModifiers(gun.Owner);
    }

    private void OnGunSelected(Entity<CMUMedicalGunAimPenaltyComponent> gun, ref HandSelectedEvent args)
    {
        if (!HasComp<CMUHumanMedicalComponent>(args.User))
            return;

        _gun.RefreshModifiers(gun.Owner);
    }

    private void RefreshGunForUser(Entity<GunComponent> gun, EntityUid user)
    {
        if (!HasComp<CMUHumanMedicalComponent>(user))
            return;

        EnsureComp<CMUMedicalGunAimPenaltyComponent>(gun.Owner);
        _gun.RefreshModifiers(gun.Owner);
    }
}
