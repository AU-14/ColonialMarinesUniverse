using System.Numerics;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Weapons.Ranged.Vulture;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Weapons.Ranged.Vulture;

public sealed partial class VultureAimOverlay : Overlay
{
    private static readonly Color SniperMask = Color.Black;
    private static readonly Color SniperBox = Color.Red.WithAlpha(0.8f);
    private static readonly Color SpotterBox = Color.Yellow.WithAlpha(0.8f);
    private static readonly Color SpotterSniperOutline = Color.Yellow.WithAlpha(0.95f);
    private static readonly ResPath VultureReticleRsi = new("_RMC14/Interface/vulture_scope.rsi");
    private const string UnsteadyReticle = "vulture_unsteady";
    private const string SteadyReticle = "vulture_steady";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly SharedContainerSystem _container;
    private readonly IEntityManager _entityManager;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace | OverlaySpace.WorldSpace;

    public VultureAimOverlay(IEntityManager entityManager)
    {
        IoCManager.InjectDependencies(this);
        _entityManager = entityManager;
        _container = entityManager.System<SharedContainerSystem>();
        _sprite = entityManager.System<SpriteSystem>();
        _transform = entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!TryGetLocalAim(out var player, out var scoping, out var aim, out var isSniper))
            return;

        switch (args.Space)
        {
            case OverlaySpace.ScreenSpace:
                DrawScopeMask(args, player, scoping, aim.Comp, isSniper);
                break;
            case OverlaySpace.WorldSpace:
                DrawWorld(args, player, scoping, aim.Comp, isSniper);
                break;
        }
    }

    private void DrawScopeMask(
        in OverlayDrawArgs args,
        EntityUid player,
        ScopingComponent scoping,
        VultureAimComponent aim,
        bool isSniper)
    {
        if (!TryGetViewCenter(player, scoping, aim, out var center) ||
            center.MapId != args.MapId)
        {
            return;
        }

        var radius = aim.BoxRadius + 0.5f;
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var corners = new[]
        {
            center.Position + new Vector2(-radius, -radius),
            center.Position + new Vector2(-radius, radius),
            center.Position + new Vector2(radius, -radius),
            center.Position + new Vector2(radius, radius),
        };

        foreach (var corner in corners)
        {
            var screen = args.ViewportControl?.WorldToScreen(corner) ?? _eye.WorldToScreen(corner);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }

        var viewportBounds = args.ViewportBounds;
        var viewport = new UIBox2(
            viewportBounds.Left,
            viewportBounds.Top,
            viewportBounds.Right,
            viewportBounds.Bottom);
        min = Vector2.Clamp(min, viewport.TopLeft, viewport.BottomRight);
        max = Vector2.Clamp(max, viewport.TopLeft, viewport.BottomRight);

        var clear = new UIBox2(min, max);
        var handle = args.ScreenHandle;
        var boxColor = isSniper ? SniperBox : SpotterBox;

        if (isSniper)
        {
            if (clear.Top > viewport.Top)
                handle.DrawRect(new UIBox2(viewport.Left, viewport.Top, viewport.Right, clear.Top), SniperMask);

            if (clear.Bottom < viewport.Bottom)
                handle.DrawRect(new UIBox2(viewport.Left, clear.Bottom, viewport.Right, viewport.Bottom), SniperMask);

            if (clear.Left > viewport.Left)
                handle.DrawRect(new UIBox2(viewport.Left, clear.Top, clear.Left, clear.Bottom), SniperMask);

            if (clear.Right < viewport.Right)
                handle.DrawRect(new UIBox2(clear.Right, clear.Top, viewport.Right, clear.Bottom), SniperMask);
        }

        handle.DrawRect(clear, boxColor, false);
    }

    private void DrawWorld(
        in OverlayDrawArgs args,
        EntityUid player,
        ScopingComponent scoping,
        VultureAimComponent aim,
        bool isSniper)
    {
        if (!TryGetViewCenter(player, scoping, aim, out var center) ||
            !TryGetCrosshairCoordinates(player, scoping, aim, out var crosshair) ||
            center.MapId != args.MapId ||
            crosshair.MapId != args.MapId)
        {
            return;
        }

        var handle = args.WorldHandle;
        var diameter = aim.BoxRadius * 2 + 1;
        var box = Box2.CenteredAround(center.Position, new Vector2(diameter, diameter));
        handle.DrawRect(box, isSniper ? SniperBox : SpotterBox, false);

        if (!isSniper)
            DrawSniperOutline(args, aim);

        DrawReticle(handle, crosshair.Position, aim);
    }

    private void DrawReticle(DrawingHandleWorld handle, Vector2 position, VultureAimComponent aim)
    {
        var state = aim.BreathEndsAt > _timing.CurTime
            ? SteadyReticle
            : UnsteadyReticle;
        var rsi = _sprite.GetState(new SpriteSpecifier.Rsi(VultureReticleRsi, state));
        var texture = rsi.GetFrame(RsiDirection.South, 0);
        var offset = new Vector2(
            texture.Width / 2f / EyeManager.PixelsPerMeter,
            texture.Height / 2f / EyeManager.PixelsPerMeter);

        handle.DrawTexture(texture, position - offset);
    }

    private void DrawSniperOutline(in OverlayDrawArgs args, VultureAimComponent aim)
    {
        if (aim.Sniper is not { } sniper ||
            !_entityManager.EntityExists(sniper))
        {
            return;
        }

        var sniperMap = _transform.GetMapCoordinates(sniper);
        if (sniperMap.MapId != args.MapId)
            return;

        var pulse = 0.7f + 0.25f * MathF.Sin((float) _timing.CurTime.TotalSeconds * 6f);
        var color = SpotterSniperOutline.WithAlpha(pulse);
        var box = Box2.CenteredAround(sniperMap.Position, new Vector2(1.1f, 1.1f));

        args.WorldHandle.DrawRect(box, color, false);
    }

    private bool TryGetLocalAim(
        out EntityUid player,
        out ScopingComponent scoping,
        out Entity<VultureAimComponent> aim,
        out bool isSniper)
    {
        if (_player.LocalEntity is not { } local ||
            !_entityManager.TryGetComponent(local, out ScopingComponent? scopingComp) ||
            scopingComp.Scope == null)
        {
            player = default;
            scoping = default!;
            aim = default;
            isSniper = false;
            return false;
        }

        scoping = scopingComp;

        var query = _entityManager.EntityQueryEnumerator<VultureAimComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            var ent = (uid, component);
            if (IsUsingRifleScope(ent, scoping.Scope))
            {
                player = local;
                aim = ent;
                isSniper = true;
                return true;
            }

            if ((component.Spotter == local ||
                 component.Spotter == null) &&
                component.SpotterScope == scoping.Scope)
            {
                player = local;
                aim = ent;
                isSniper = false;
                return true;
            }

            if (TryGetMountedSpotterTripod(scoping.Scope.Value, out var tripod) &&
                IsTripodLinkedToAim(ent, tripod))
            {
                player = local;
                aim = ent;
                isSniper = false;
                return true;
            }
        }

        player = default;
        aim = default;
        isSniper = false;
        return false;
    }

    private bool TryGetViewCenter(
        EntityUid player,
        ScopingComponent scoping,
        VultureAimComponent aim,
        out MapCoordinates center)
    {
        if (aim.ViewCenter is { } current)
        {
            center = current;
            return true;
        }

        if (scoping.Scope is not { } scope ||
            !_entityManager.TryGetComponent(scope, out ScopeComponent? scopeComp))
        {
            center = default;
            return false;
        }

        var playerMap = _transform.GetMapCoordinates(player);
        var direction = scopeComp.ScopingDirection ?? _transform.GetWorldRotation(player).GetCardinalDir();
        var forward = direction.ToVec();
        var side = new Vector2(-forward.Y, forward.X);
        var position = playerMap.Position +
                       forward * (aim.BaseViewDistance + aim.ViewForwardOffset) +
                       side * aim.ViewSideOffset;

        center = Centered(position, playerMap.MapId);
        return true;
    }

    private bool TryGetCrosshairCoordinates(
        EntityUid player,
        ScopingComponent scoping,
        VultureAimComponent aim,
        out MapCoordinates crosshair)
    {
        if (aim.CrosshairCoordinates is { } current)
        {
            crosshair = current;
            return true;
        }

        if (!TryGetViewCenter(player, scoping, aim, out var center))
        {
            crosshair = default;
            return false;
        }

        var reticle = GetReticleOffset(aim);

        crosshair = new MapCoordinates(center.Position + reticle, center.MapId);
        return true;
    }

    private bool IsUsingRifleScope(Entity<VultureAimComponent> aim, EntityUid? scope)
    {
        if (scope == null)
            return false;

        if (aim.Comp.RifleScope == scope)
            return true;

        return _container.TryGetContainingContainer(scope.Value, out var container) &&
               container.Owner == aim.Owner;
    }

    private bool TryGetMountedSpotterTripod(EntityUid scope, out EntityUid tripod)
    {
        if (_container.TryGetContainingContainer(scope, out var container) &&
            _entityManager.HasComponent<VultureSpotterTripodComponent>(container.Owner))
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
            !_entityManager.EntityExists(sniper) ||
            !_entityManager.EntityExists(tripod))
        {
            return false;
        }

        var sniperMap = _transform.GetMapCoordinates(sniper);
        var tripodMap = _transform.GetMapCoordinates(tripod);
        if (sniperMap.MapId != tripodMap.MapId)
            return false;

        return (sniperMap.Position - tripodMap.Position).LengthSquared() <= aim.Comp.SpotterLinkRange * aim.Comp.SpotterLinkRange;
    }

    private static MapCoordinates Centered(Vector2 position, MapId mapId)
    {
        return new MapCoordinates(
            new Vector2(MathF.Floor(position.X) + 0.5f, MathF.Floor(position.Y) + 0.5f),
            mapId);
    }

    private static Vector2 GetReticleOffset(VultureAimComponent aim)
    {
        var reticle = new Vector2(
            aim.CrosshairAdjust.X + aim.SwayOffset.X,
            aim.CrosshairAdjust.Y + aim.SwayOffset.Y) + aim.DriftOffset;

        return new Vector2(
            Math.Clamp(reticle.X, -aim.BoxRadius, aim.BoxRadius),
            Math.Clamp(reticle.Y, -aim.BoxRadius, aim.BoxRadius));
    }
}
