using System.Numerics;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Weapons.Ranged.Vulture;
using Content.Shared.Camera;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Client._RMC14.Weapons.Ranged.Vulture;

public sealed partial class VultureAimSystem : SharedVultureAimSystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScopingComponent, GetEyeOffsetRelayedEvent>(OnGetEyeOffset);

        _overlays.AddOverlay(new VultureAimOverlay(EntityManager));
    }

    private void OnGetEyeOffset(Entity<ScopingComponent> ent, ref GetEyeOffsetRelayedEvent args)
    {
        if (_player.LocalEntity != ent.Owner ||
            !TryGetLocalAim(ent, out var aim) ||
            !TryGetViewCenter(ent, aim.Comp, out var center))
            return;

        var playerMap = _transform.GetMapCoordinates(ent.Owner);
        if (playerMap.MapId != center.MapId)
            return;

        args.Offset += center.Position - playerMap.Position - ent.Comp.EyeOffset;
    }

    private bool TryGetLocalAim(Entity<ScopingComponent> player, out Entity<VultureAimComponent> aim)
    {
        var query = EntityQueryEnumerator<VultureAimComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            var ent = (uid, component);
            if (!IsUsingVultureScope(ent, player.Comp.Scope))
                continue;

            aim = ent;
            return true;
        }

        aim = default;
        return false;
    }

    private bool TryGetViewCenter(Entity<ScopingComponent> player, VultureAimComponent aim, out MapCoordinates center)
    {
        if (aim.ViewCenter is { } current)
        {
            center = current;
            return true;
        }

        if (player.Comp.Scope is not { } scope ||
            !TryComp(scope, out ScopeComponent? scopeComp))
        {
            center = default;
            return false;
        }

        var playerMap = _transform.GetMapCoordinates(player.Owner);
        var direction = scopeComp.ScopingDirection ?? _transform.GetWorldRotation(player.Owner).GetCardinalDir();
        var forward = direction.ToVec();
        var side = new Vector2(-forward.Y, forward.X);
        var position = playerMap.Position +
                       forward * (aim.BaseViewDistance + aim.ViewForwardOffset) +
                       side * aim.ViewSideOffset;

        center = Centered(position, playerMap.MapId);
        return true;
    }

    private bool IsUsingVultureScope(Entity<VultureAimComponent> aim, EntityUid? scope)
    {
        if (scope == null)
            return false;

        if (aim.Comp.RifleScope == scope ||
            aim.Comp.SpotterScope == scope)
        {
            return true;
        }

        if (TryGetMountedSpotterTripod(scope.Value, out var tripod) &&
            IsTripodLinkedToAim(aim, tripod))
        {
            return true;
        }

        return _container.TryGetContainingContainer(scope.Value, out var container) &&
               container.Owner == aim.Owner;
    }

    private bool TryGetMountedSpotterTripod(EntityUid scope, out EntityUid tripod)
    {
        if (_container.TryGetContainingContainer(scope, out var container) &&
            HasComp<VultureSpotterTripodComponent>(container.Owner))
        {
            tripod = container.Owner;
            return true;
        }

        tripod = default;
        return false;
    }

    private bool IsTripodLinkedToAim(Entity<VultureAimComponent> aim, EntityUid tripod)
    {
        if (aim.Comp.Sniper is not { } sniper ||
            aim.Comp.ViewCenter == null ||
            TerminatingOrDeleted(sniper) ||
            TerminatingOrDeleted(tripod))
        {
            return false;
        }

        var sniperMap = _transform.GetMapCoordinates(sniper);
        var tripodMap = _transform.GetMapCoordinates(tripod);
        if (sniperMap.MapId != tripodMap.MapId)
            return false;

        return (sniperMap.Position - tripodMap.Position).LengthSquared() <= aim.Comp.SpotterLinkRange * aim.Comp.SpotterLinkRange;
    }

}
