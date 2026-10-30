using System.Numerics;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged.SmartGun;
using Content.Shared.CMU14.Visor;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Visor;

/// <summary>
/// Ported from TGMC's ammo HUD: while a squad optic is down, shows what the held gun is loaded with and how many rounds
/// it has left, under the status alerts on the right of the screen. The smartgun's head mounted sight shows it too.
/// </summary>
public sealed class CMUAmmoHudOverlay : Overlay
{
    public static readonly ResPath Rsi = new("/Textures/CMU14/Interface/ammo_hud.rsi");

    private const int Scale = 2;
    private const int Margin = 12;
    private const int AlertGap = 4;

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private readonly SharedHandsSystem _hands;
    private readonly InventorySystem _inventory;
    private readonly SpriteSystem _sprite;

    // Which icon a gun prototype gets never changes, so work it out once.
    private readonly Dictionary<string, string> _typeCache = new();

    private readonly List<SpriteSpecifier.Rsi> _layers = new(5);
    private int _lastRounds = -1;
    private string? _lastType;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CMUAmmoHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        _hands = _entity.System<SharedHandsSystem>();
        _inventory = _entity.System<InventorySystem>();
        _sprite = _entity.System<SpriteSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // screen overlays run once per viewport, so camera feeds like the TV would get a copy drawn on top
        if (args.ViewportControl != _eye.MainViewport)
            return false;

        if (_player.LocalEntity is not { } player)
            return false;

        if (_entity.TryGetComponent(player, out CMUSquadVisorWearerComponent? wearer) && wearer.AmmoHudEnabled)
            return true;

        return _inventory.TryGetSlotEntity(player, "eyes", out var eyes) &&
               _entity.HasComponent<CMUAmmoHudSightComponent>(eyes);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player ||
            _hands.GetActiveItem(player) is not { } held ||
            !_entity.HasComponent<GunComponent>(held))
        {
            return;
        }

        var ev = new GetAmmoCountEvent();
        _entity.EventBus.RaiseLocalEvent(held, ref ev);
        var rounds = Math.Clamp(ev.Count, 0, 999);

        UpdateLayers(rounds, GetAmmoType(held));
        var box = GetBox(args.ViewportBounds);

        // Keep sampling animated states every frame, even when the layer selection is unchanged.
        var time = _timing.CurTime;
        foreach (var layer in _layers)
        {
            var texture = _sprite.GetFrame(layer, time);
            args.ScreenHandle.DrawTextureRect(texture, box);
        }
    }

    private void UpdateLayers(int rounds, string type)
    {
        if (_lastRounds == rounds && _lastType == type)
            return;

        _lastRounds = rounds;
        _lastType = type;
        _layers.Clear();
        AddLayer("ammo");
        if (rounds == 0)
        {
            AddLayer(EmptyState(type));
            AddLayer("o0_flash");
        }
        else
        {
            AddLayer(type);
            AddLayer("o" + (rounds % 10));
            if (rounds >= 10)
                AddLayer("t" + (rounds / 10 % 10));
            if (rounds >= 100)
                AddLayer("h" + (rounds / 100));
        }
    }

    private void AddLayer(string state)
    {
        _layers.Add(new SpriteSpecifier.Rsi(Rsi, state));
    }

    /// <summary>
    ///     Sits in the alerts column, right-aligned with the alert icons and just below the last one, at the same size.
    ///     Falls back to the bottom right of the viewport if the alerts aren't on screen.
    /// </summary>
    private UIBox2 GetBox(UIBox2i viewport)
    {
        if (_ui.GetActiveUIWidgetOrNull<AlertsUI>() is { } alerts && alerts.VisibleInTree)
        {
            var uiScale = alerts.UIScale;
            var size = 32 * Scale * uiScale;
            var container = alerts.AlertContainer;
            var right = container.GlobalPixelPosition.X + container.PixelSize.X;
            if (container.PixelSize.X <= 0)
                right = alerts.GlobalPixelPosition.X + alerts.PixelSize.X;

            var top = container.GlobalPixelPosition.Y + container.PixelSize.Y + AlertGap * uiScale;
            return UIBox2.FromDimensions(new Vector2(right - size, top), new Vector2(size, size));
        }

        const int fallbackSize = 32 * Scale;
        var topLeft = new Vector2(viewport.Right - fallbackSize - Margin, viewport.Bottom - fallbackSize - Margin);
        return UIBox2.FromDimensions(topLeft, new Vector2(fallbackSize, fallbackSize));
    }

    private static string EmptyState(string type)
    {
        return type switch
        {
            "minigun" => "minigun_empty",
            "taser" => "battery_empty_flash",
            "rocket_he" => "rocket_empty_flash",
            "shotgun_buckshot" => "shotgun_empty_flash",
            "grenade_he" => "grenade_empty_flash",
            "unknown" => "unknown",
            _ => type + "_empty_flash",
        };
    }

    private string GetAmmoType(EntityUid gun)
    {
        if (_entity.HasComponent<SmartGunComponent>(gun))
            return "smartgun";

        if (_entity.HasComponent<RMCFlamerAmmoProviderComponent>(gun))
            return "flame";

        if (_entity.HasComponent<BatteryAmmoProviderComponent>(gun))
            return "taser";

        if (_entity.GetComponent<MetaDataComponent>(gun).EntityPrototype is not { } proto)
            return "rifle";

        if (_typeCache.TryGetValue(proto.ID, out var cached))
            return cached;

        var names = proto.ID.ToLowerInvariant();
        foreach (var parent in _prototype.EnumerateParents<EntityPrototype>(proto.ID))
        {
            names += " " + parent.ID.ToLowerInvariant();
        }

        string type;
        if (names.Contains("shotgun"))
            type = "shotgun_buckshot";
        else if (names.Contains("grenadelauncher"))
            type = "grenade_he";
        else if (names.Contains("rocket") || names.Contains("launcher"))
            type = "rocket_he";
        else if (names.Contains("minigun"))
            type = "minigun";
        else if (names.Contains("sniper"))
            type = "sniper";
        else if (names.Contains("revolver") || _entity.HasComponent<RevolverAmmoProviderComponent>(gun))
            type = "revolver";
        else if (names.Contains("smg"))
            type = "smg";
        else if (names.Contains("pistol"))
            type = "pistol";
        else
            type = "rifle";

        _typeCache[proto.ID] = type;
        return type;
    }
}
