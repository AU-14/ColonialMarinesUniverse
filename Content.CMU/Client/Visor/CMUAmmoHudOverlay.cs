using System.Numerics;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged.SmartGun;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Visor;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Visor;

/// <summary>
/// Ported from TGMC's ammo HUD: while a squad optic is down, shows what the held gun is loaded with and how many rounds
/// it has left in the corner of the screen.
/// </summary>
public sealed class CMUAmmoHudOverlay : Overlay
{
    public static readonly ResPath Rsi = new("/Textures/CMU14/Interface/ammo_hud.rsi");

    private const int Scale = 2;
    private const int Margin = 12;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SharedHandsSystem _hands;
    private readonly SpriteSystem _sprite;

    // Which icon a gun prototype gets never changes, so work it out once.
    private readonly Dictionary<string, string> _typeCache = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CMUAmmoHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        _hands = _entity.System<SharedHandsSystem>();
        _sprite = _entity.System<SpriteSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _config.GetCVar(CCVars.CMUAmmoHud) &&
               _player.LocalEntity is { } player &&
               _entity.HasComponent<CMUSquadVisorWearerComponent>(player);
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

        var type = GetAmmoType(held);
        var layers = new List<string> { "ammo" };
        if (rounds == 0)
        {
            layers.Add(EmptyState(type));
            layers.Add("o0_flash");
        }
        else
        {
            layers.Add(type);
            layers.Add("o" + (rounds % 10));
            if (rounds >= 10)
                layers.Add("t" + (rounds / 10 % 10));
            if (rounds >= 100)
                layers.Add("h" + (rounds / 100));
        }

        const int size = 32 * Scale;
        var bounds = args.ViewportBounds;
        var topLeft = new Vector2(bounds.Right - size - Margin, bounds.Bottom - size - Margin);
        var box = UIBox2.FromDimensions(topLeft, new Vector2(size, size));

        var time = _timing.CurTime;
        foreach (var state in layers)
        {
            var texture = _sprite.GetFrame(new SpriteSpecifier.Rsi(Rsi, state), time);
            args.ScreenHandle.DrawTextureRect(texture, box);
        }
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
