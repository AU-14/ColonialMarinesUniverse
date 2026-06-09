using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged.Vulture;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Weapons.Ranged.Vulture;

public sealed partial class VultureRifleSystem : EntitySystem
{
    [Dependency] private AttachableHolderSystem _attachableHolder = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VultureRifleComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<VultureProjectileComponent, AfterProjectileHitEvent>(OnProjectileHit);
    }

    private void OnGunShot(Entity<VultureRifleComponent> ent, ref GunShotEvent args)
    {
        if (HasDeployedBipod(ent))
            return;

        _popup.PopupEntity(
            Loc.GetString("rmc-vulture-unbraced-user", ("gun", ent.Owner)),
            ent.Owner,
            args.User,
            PopupType.LargeCaution);

        _popup.PopupEntity(
            Loc.GetString("rmc-vulture-unbraced-others", ("user", args.User), ("gun", ent.Owner)),
            args.User,
            Filter.PvsExcept(args.User),
            true,
            PopupType.LargeCaution);

        _damageable.TryChangeDamage(args.User, ent.Comp.UnbracedDamage, origin: ent.Owner, tool: ent.Owner);
        _stun.TryKnockdown(args.User, ent.Comp.UnbracedKnockdown, true);
        _stun.TrySlowdown(args.User, ent.Comp.UnbracedSlowdown, true, ent.Comp.UnbracedWalkModifier, ent.Comp.UnbracedSprintModifier);

        var fromMap = _transform.GetMapCoordinates(args.User);
        var toMap = _transform.ToMapCoordinates(args.ToCoordinates);
        if (fromMap.MapId == toMap.MapId)
        {
            _sizeStun.KnockBack(
                args.User,
                toMap,
                ent.Comp.UnbracedKnockback,
                ent.Comp.UnbracedKnockback,
                ent.Comp.UnbracedKnockbackSpeed,
            true);
        }
    }

    private void OnProjectileHit(Entity<VultureProjectileComponent> ent, ref AfterProjectileHitEvent args)
    {
        _audio.PlayPvs(ent.Comp.ReportSound, args.Target);
    }

    private bool HasDeployedBipod(Entity<VultureRifleComponent> ent)
    {
        if (!TryComp(ent.Owner, out AttachableHolderComponent? holder))
            return false;

        if (!_attachableHolder.TryGetAttachable((ent.Owner, holder), ent.Comp.BipodSlot, out var attachable))
            return false;

        return TryComp(attachable.Owner, out AttachableToggleableComponent? toggleable) && toggleable.Active;
    }
}
