using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Targeting;
using Content.Shared._RMC14.Weapons.Ranged.Vulture;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Ranged.Vulture;

public sealed partial class CMUVultureWindupSystem : EntitySystem
{
    [Dependency] private AttachableHolderSystem _attachableHolder = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        // after the Vulture scope swaps the click for the crosshair, so the mark and the spread use the real aim point
        SubscribeLocalEvent<CMUVultureWindupComponent, AttemptShootEvent>(OnAttemptShoot, after: [typeof(SharedVultureAimSystem)]);
    }

    private void OnAttemptShoot(Entity<CMUVultureWindupComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || args.ToCoordinates is not { } to)
            return;

        if (ent.Comp.Releasing)
        {
            if (!IsBraced(ent))
                args.ToCoordinates = Spread(ent, args.FromCoordinates, to);
            return;
        }

        // the client never fires this gun itself, the server does it once the windup is done.
        // ResetCooldown so the swallowed trigger pull doesn't burn the 4s bolt cycle
        args.Cancelled = true;
        args.ResetCooldown = true;

        if (_net.IsClient || ent.Comp.WindupEndsAt != null)
            return;

        StartWindup(ent, args.User, to);
    }

    private void StartWindup(Entity<CMUVultureWindupComponent> ent, EntityUid user, EntityCoordinates aim)
    {
        ent.Comp.WindupEndsAt = _timing.CurTime + ent.Comp.WindupTime;
        ent.Comp.User = user;
        ent.Comp.AimCoordinates = aim;
        ent.Comp.UserOrigin = _transform.GetMapCoordinates(user);
        ent.Comp.MarkedTarget = FindMarkTarget(ent, user, aim);

        if (ent.Comp.MarkedTarget is { } target)
        {
            // a bit longer than the windup so the laser is still up when the round leaves
            var duration = (float) ent.Comp.WindupTime.TotalSeconds + 0.25f;
            _targeting.Target(ent, user, target, duration, ent.Comp.MarkEffect, showDirection: true);
        }

        _audio.PlayPvs(ent.Comp.WindupSound, ent);
        _popup.PopupEntity(Loc.GetString("cmu-vulture-windup-start", ("gun", ent.Owner)), user, user);
    }

    private EntityUid? FindMarkTarget(Entity<CMUVultureWindupComponent> ent, EntityUid user, EntityCoordinates aim)
    {
        var aimMap = _transform.ToMapCoordinates(aim);
        _mobs.Clear();
        _lookup.GetEntitiesInRange(aimMap, ent.Comp.MarkRadius, _mobs);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var mob in _mobs)
        {
            if (mob.Owner == user)
                continue;

            var distance = (_transform.GetMapCoordinates(mob).Position - aimMap.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = mob;
            bestDistance = distance;
        }

        return best;
    }

    private EntityCoordinates Spread(Entity<CMUVultureWindupComponent> ent, EntityCoordinates from, EntityCoordinates to)
    {
        var fromMap = _transform.ToMapCoordinates(from);
        var toMap = _transform.ToMapCoordinates(to);
        if (fromMap.MapId != toMap.MapId)
            return to;

        var spread = Angle.FromDegrees(_random.NextFloat(-ent.Comp.UnbracedSpreadDegrees, ent.Comp.UnbracedSpreadDegrees));
        var offset = spread.RotateVec(toMap.Position - fromMap.Position);
        return _transform.ToCoordinates(new MapCoordinates(fromMap.Position + offset, fromMap.MapId));
    }

    private bool IsBraced(Entity<CMUVultureWindupComponent> ent)
    {
        return TryComp(ent, out AttachableHolderComponent? holder) &&
               _attachableHolder.TryGetAttachable((ent, holder), ent.Comp.BipodSlot, out var bipod) &&
               TryComp(bipod, out AttachableToggleableComponent? toggleable) &&
               toggleable.Active;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUVultureWindupComponent, GunComponent>();
        while (query.MoveNext(out var uid, out var windup, out var gun))
        {
            if (windup.WindupEndsAt is not { } endsAt)
                continue;

            var ent = (uid, windup);
            if (!StillSteady(ent))
            {
                if (windup.User is { } user)
                    _popup.PopupEntity(Loc.GetString("cmu-vulture-windup-broken", ("gun", uid)), user, user, PopupType.SmallCaution);

                ClearWindup(ent);
                continue;
            }

            if (endsAt > now)
                continue;

            Fire(ent, (uid, gun));
        }
    }

    private bool StillSteady(Entity<CMUVultureWindupComponent> ent)
    {
        if (ent.Comp.User is not { } user ||
            TerminatingOrDeleted(user) ||
            !_hands.IsHolding(user, ent.Owner) ||
            TryComp(ent, out WieldableComponent? wieldable) && !wieldable.Wielded)
        {
            return false;
        }

        if (ent.Comp.UserOrigin is not { } origin)
            return false;

        var current = _transform.GetMapCoordinates(user);
        return current.MapId == origin.MapId && (current.Position - origin.Position).LengthSquared() <= 0.01f;
    }

    private void Fire(Entity<CMUVultureWindupComponent> ent, Entity<GunComponent> gun)
    {
        var user = ent.Comp.User!.Value;
        var aim = ent.Comp.AimCoordinates!.Value;

        // the round goes where the crosshair is now, not where the mark is. stepping out of it is the counterplay
        ent.Comp.Releasing = true;
        var projectiles = _gun.AttemptShoot(gun, user, aim);
        ent.Comp.Releasing = false;

        // the shooter's client never predicted this one, so it needs its own bang
        if (projectiles != null)
            _audio.PlayEntity(gun.Comp.SoundGunshotModified, user, ent);

        _gun.UpdateAmmoCount(ent);
        ClearWindup(ent);
    }

    private void ClearWindup(Entity<CMUVultureWindupComponent> ent)
    {
        if (ent.Comp.MarkedTarget is { } target)
            _targeting.StopTargeting(ent.Owner, target);

        ent.Comp.WindupEndsAt = null;
        ent.Comp.User = null;
        ent.Comp.AimCoordinates = null;
        ent.Comp.UserOrigin = null;
        ent.Comp.MarkedTarget = null;
    }
}
