using Content.Shared._RMC14.Emplacements;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.CombatMode;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship.AttachmentPoint;

public sealed partial class CMUDoorGunAimRelaySystem : EntitySystem
{
    [Dependency] private CMUDoorGunAimSystem _aim = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUDoorGunAimEvent>(OnAim);
    }

    private void OnAim(CMUDoorGunAimEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } user
            || !_aim.TryGetMount(user, out var mount, out var visual)
            || _timing.CurTime < visual.NextAimUpdate
            || !TryGetEntity(args.Coordinates.NetEntity, out _))
            return;

        visual.NextAimUpdate = _timing.CurTime + TimeSpan.FromMilliseconds(50);
        if (!_aim.TryGetAimOffset(mount, GetCoordinates(args.Coordinates), out var offset))
            return;

        if (visual.Operator != user)
        {
            visual.Operator = user;
            Dirty(mount, visual);
        }
        SetAim(mount, visual, offset);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUDoorGunVisualComponent, WeaponMountComponent>();
        while (query.MoveNext(out var uid, out var visual, out var mount))
        {
            var activeOperator = _combat.IsInCombatMode(mount.User) ? mount.User : null;
            if (activeOperator == visual.Operator)
                continue;

            visual.Operator = activeOperator;
            visual.NextAimUpdate = TimeSpan.Zero;
            visual.AimOffset = Angle.Zero;
            Dirty(uid, visual);
        }
    }

    private void SetAim(EntityUid mount, CMUDoorGunVisualComponent visual, Angle offset)
    {
        if (visual.AimOffset == offset)
            return;

        visual.AimOffset = offset;
        Dirty(mount, visual);
    }
}
