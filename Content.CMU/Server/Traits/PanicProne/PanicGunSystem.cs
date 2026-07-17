using System.Collections.Generic;
using Content.Shared._CMU14.Traits.PanicProne;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._CMU14.Traits.PanicProne;

public sealed partial class PanicGunSystem : PanicSystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private readonly HashSet<EntityUid> _refreshedGuns = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PanicGunAimPenaltyComponent, GotUnequippedHandEvent>(OnGunUnequipped);
        SubscribeLocalEvent<PanicGunAimPenaltyComponent, HandSelectedEvent>(OnGunSelected);
    }

    protected override void RefreshAimDependentWeapons(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        var peaked = TryComp<PanicComponent>(body, out var panic) && panic.Peaked;

        _refreshedGuns.Clear();
        foreach (var held in _hands.EnumerateHeld((body, hands)))
        {
            if (!_gun.TryGetGun(held, out var gunUid, out var gun))
                continue;

            if (!_refreshedGuns.Add(gunUid))
                continue;

            if (peaked)
                EnsureComp<PanicGunAimPenaltyComponent>(gunUid);
            else
                RemComp<PanicGunAimPenaltyComponent>(gunUid);

            _gun.RefreshModifiers((gunUid, gun));
        }

        _refreshedGuns.Clear();
    }

    private void OnGunUnequipped(Entity<PanicGunAimPenaltyComponent> gun, ref GotUnequippedHandEvent args)
    {
        RemComp<PanicGunAimPenaltyComponent>(gun.Owner);
        _gun.RefreshModifiers(gun.Owner);
    }

    private void OnGunSelected(Entity<PanicGunAimPenaltyComponent> gun, ref HandSelectedEvent args)
    {
        if (!TryComp<PanicComponent>(args.User, out var panic) || !panic.Peaked)
            return;

        _gun.RefreshModifiers(gun.Owner);
    }
}
