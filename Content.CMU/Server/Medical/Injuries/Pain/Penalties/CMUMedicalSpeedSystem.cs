using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Injuries.Pain.Penalties;

public sealed partial class CMUMedicalSpeedSystem : SharedCMUMedicalSpeedSystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private readonly HashSet<EntityUid> _refreshedGuns = new();

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

        _refreshedGuns.Clear();
        foreach (var held in _hands.EnumerateHeld((body, hands)))
        {
            if (!_gun.TryGetGun(held, out var gunUid, out GunComponent? gun))
                continue;

            if (!_refreshedGuns.Add(gunUid))
                continue;

            EnsureComp<CMUMedicalGunAimPenaltyComponent>(gunUid);
            _gun.RefreshModifiers((gunUid, gun));
        }

        _refreshedGuns.Clear();
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
