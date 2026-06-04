using System.Numerics;
using Content.Client._RMC14.Emplacements;
using Content.Client.Hands.Systems;
using Content.Client.Resources;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.CombatMode;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Utility;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.CombatMode;

/// <summary>
///   This shows something like crosshairs for the combat mode next to the mouse cursor.
///   For weapons with the gun class, a crosshair of one type is displayed,
///   while for all other types of weapons and items in hand, as well as for an empty hand,
///   a crosshair of a different type is displayed. These crosshairs simply show the state of combat mode (on|off).
/// </summary>
public sealed class CombatModeIndicatorsOverlay : Overlay
{
    private readonly IInputManager _inputManager;
    private readonly IEntityManager _entMan;
    private readonly IEyeManager _eye;
    private readonly IPlayerManager _player;
    private readonly CombatModeSystem _combat;
    private readonly HandsSystem _hands = default!;
    private readonly RMCCombatModeSystem _rmcCombatMode;
    private readonly SpriteSystem _sprite;
    private readonly RMCWeaponControllerSystem _rmcWeaponController;

    private readonly Texture _gunSight;
    private readonly Texture _gunBoltSight;
    private readonly Texture _meleeSight;
    private readonly Font _zLevelIndicatorFont;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public Color MainColor = Color.White.WithAlpha(0.3f);
    public Color StrokeColor = Color.Black.WithAlpha(0.5f);
    public Color ZLevelIndicatorColor = Color.White.WithAlpha(0.95f);
    public Color ZLevelIndicatorStrokeColor = Color.Black.WithAlpha(0.8f);
    public float Scale = 0.6f;  // 1 is a little big

    public CombatModeIndicatorsOverlay(IInputManager input, IEntityManager entMan,
            IEyeManager eye, CombatModeSystem combatSys, HandsSystem hands,
            IPlayerManager player, IResourceCache resourceCache)
    {
        _inputManager = input;
        _entMan = entMan;
        _eye = eye;
        _player = player;
        _combat = combatSys;
        _hands = hands;
        _zLevelIndicatorFont = resourceCache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 20);

        var spriteSys = _entMan.EntitySysManager.GetEntitySystem<SpriteSystem>();
        _gunSight = spriteSys.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Misc/crosshair_pointers.rsi"),
            "gun_sight"));
        _gunBoltSight = spriteSys.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Misc/crosshair_pointers.rsi"),
            "gun_bolt_sight"));
        _meleeSight = spriteSys.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Misc/crosshair_pointers.rsi"),
             "melee_sight"));

        _rmcCombatMode = entMan.System<RMCCombatModeSystem>();
        _sprite = entMan.System<SpriteSystem>();
        _rmcWeaponController = entMan.System<RMCWeaponControllerSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_combat.IsInCombatMode())
            return false;

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var mouseScreenPosition = _inputManager.MouseScreenPosition;
        var mousePosMap = _eye.PixelToMap(mouseScreenPosition);
        if (mousePosMap.MapId != args.MapId)
            return;

        var handEntity = _hands.GetActiveHandEntity();
        var isHandGunItem = _entMan.HasComponent<GunComponent>(handEntity);
        var isGunBolted = true;
        if (_entMan.TryGetComponent(handEntity, out ChamberMagazineAmmoProviderComponent? chamber))
            isGunBolted = chamber.BoltClosed ?? true;


        var mousePos = mouseScreenPosition.Position;
        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;
        var limitedScale = uiScale > 1.25f ? 1.25f : uiScale;

        // RMC14
        var crosshairEntity = handEntity;
        if (_rmcWeaponController.TryGetControllingWeapon(out var weapon))
            crosshairEntity = weapon;

        var sight = isHandGunItem ? (isGunBolted ? _gunSight : _gunBoltSight) : _meleeSight;
        var crosshairHeight = sight.Size.Y * limitedScale * Scale + 7f;
        if (crosshairEntity != null && _rmcCombatMode.GetCrosshair(crosshairEntity.Value) is { } crosshair)
        {
            sight = _sprite.Frame0(crosshair);
            var sightSize = sight.Size * limitedScale;
            var rect = UIBox2.FromDimensions(mousePos - sightSize * 0.5f, sightSize);
            args.ScreenHandle.DrawTextureRect(sight, rect);
            crosshairHeight = sightSize.Y;
        }
        else
        {
            DrawSight(sight, args.ScreenHandle, mousePos, limitedScale * Scale);
        }

        DrawZLevelIndicator(args.ScreenHandle, mousePos, crosshairHeight * 0.5f, limitedScale, crosshairEntity);
    }

    private void DrawSight(Texture sight, DrawingHandleScreen screen, Vector2 centerPos, float scale)
    {
        var sightSize = sight.Size * scale;
        var expandedSize = sightSize + new Vector2(7f, 7f);

        screen.DrawTextureRect(sight,
            UIBox2.FromDimensions(centerPos - sightSize * 0.5f, sightSize), StrokeColor);
        screen.DrawTextureRect(sight,
            UIBox2.FromDimensions(centerPos - expandedSize * 0.5f, expandedSize), MainColor);
    }

    private void DrawZLevelIndicator(
        DrawingHandleScreen screen,
        Vector2 centerPos,
        float crosshairRadius,
        float scale,
        EntityUid? crosshairEntity)
    {
        if (_player.LocalEntity is not { } player)
            return;

        var lookUp = _entMan.TryGetComponent(player, out CMUZLevelViewerComponent? viewer) && viewer.LookUp;
        var shootDown = _entMan.TryGetComponent(player, out CMUZLevelShooterComponent? shooter) && shooter.ShootDown;
        var indicator = ZLevelCrosshairIndicatorHelper.Get(
            lookUp && HasReadyGun(crosshairEntity),
            shootDown);
        var glyph = ZLevelCrosshairIndicatorHelper.GetGlyph(indicator);
        if (glyph == null)
            return;

        var direction = indicator == ZLevelCrosshairIndicator.Up ? -1f : 1f;
        var textSize = screen.GetDimensions(_zLevelIndicatorFont, glyph, scale);
        var gap = 3f * scale;
        var textCenter = centerPos + new Vector2(0f, direction * (crosshairRadius + gap + textSize.Y * 0.5f));
        var textPosition = textCenter - textSize * 0.5f;
        var shadowOffset = Vector2.One * MathF.Max(1f, scale);

        screen.DrawString(
            _zLevelIndicatorFont,
            textPosition + shadowOffset,
            glyph,
            scale,
            ZLevelIndicatorStrokeColor);
        screen.DrawString(
            _zLevelIndicatorFont,
            textPosition,
            glyph,
            scale,
            ZLevelIndicatorColor);
    }

    private bool HasReadyGun(EntityUid? uid)
    {
        if (uid == null ||
            !_entMan.HasComponent<GunComponent>(uid.Value))
        {
            return false;
        }

        return !_entMan.TryGetComponent(uid.Value, out WieldableComponent? wieldable) || wieldable.Wielded;
    }
}
