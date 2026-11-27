using System.Numerics;
using Content.Shared.CMU14.TargetLock;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.TargetLock;

/// <summary>
///     While the target lock visor is worn and flipped down, the whole screen gets CRT scanlines, and it warns the
///     wearer when the gun in their hand is running low on ammunition or when they are badly hurt.
/// </summary>
public sealed class CMUTargetLockHudSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private CMUTargetLockHudControl? _control;

    public override void Shutdown()
    {
        base.Shutdown();
        _control?.Orphan();
        _control = null;
    }

    public override void FrameUpdate(float frameTime)
    {
        var visor = FindWornVisor(_player.LocalEntity);
        if (visor is not { } active)
        {
            if (_control != null)
                _control.Visible = false;

            return;
        }

        if (_control == null)
        {
            var font = new VectorFont(_resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 26);
            _control = new CMUTargetLockHudControl(font);
            _ui.PopupRoot.AddChild(_control);
        }

        var player = _player.LocalEntity!.Value;
        _control.Visible = true;
        _control.AmmoLow = IsAmmoLow(player, active.Comp.LowAmmoFraction);
        _control.HealthLow = IsHealthLow(player, active.Comp.LowHealthFraction);
        _control.Advance(frameTime);
    }

    // The visor has to be on a helmet that is actually worn, and flipped down.
    private Entity<CMUTargetLockComponent>? FindWornVisor(EntityUid? player)
    {
        if (player is not { } wearer ||
            !_inventory.TryGetSlotEntity(wearer, "head", out var helmet) ||
            !TryComp(helmet, out StorageComponent? storage) ||
            storage.Container == null)
        {
            return null;
        }

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TryComp(item, out CMUTargetLockComponent? visor) && _itemToggle.IsActivated(item))
                return (item, visor);
        }

        return null;
    }

    private bool IsAmmoLow(EntityUid player, float fraction)
    {
        if (_hands.GetActiveItem(player) is not { } held || !HasComp<GunComponent>(held))
            return false;

        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(held, ref ev);

        return ev.Capacity > 0 && ev.Count <= ev.Capacity * fraction;
    }

    private bool IsHealthLow(EntityUid player, float fraction)
    {
        if (!TryComp(player, out DamageableComponent? damageable) ||
            !TryComp(player, out MobStateComponent? state) ||
            state.CurrentState == Shared.Mobs.MobState.Dead ||
            !_thresholds.TryGetDeadThreshold(player, out var dead) ||
            dead is not { } threshold ||
            threshold <= 0)
        {
            return false;
        }

        var remaining = 1f - (float) (_damageable.GetTotalDamage((player, damageable)) / threshold);
        return remaining <= fraction;
    }
}

/// <summary>Lays scanlines over everything on screen, and the two warnings when they apply.</summary>
public sealed class CMUTargetLockHudControl : Control
{
    private static readonly Color Line = new(0f, 0f, 0f, 0.28f);
    private static readonly Color Band = new(1f, 1f, 1f, 0.035f);
    private static readonly Color Warning = new(1f, 0.25f, 0.25f, 1f);

    private readonly Font _font;
    private float _time;

    public bool AmmoLow;
    public bool HealthLow;

    public CMUTargetLockHudControl(Font font)
    {
        _font = font;
        MouseFilter = MouseFilterMode.Ignore;
        CanKeyboardFocus = false;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    public void Advance(float frameTime)
    {
        _time += frameTime;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        // The scanlines: a dark line every few pixels across the whole screen.
        for (var y = 0; y < size.Y; y += 3)
        {
            handle.DrawRect(new UIBox2(0, y, size.X, y + 1), Line);
        }

        // A faint band rolling down the screen.
        var roll = (_time * 90f) % (size.Y + 120f) - 120f;
        handle.DrawRect(new UIBox2(0, roll, size.X, roll + 120f), Band);

        // The warnings blink, stacked under the top edge.
        if ((int) (_time * 2f) % 2 != 0)
            return;

        var top = size.Y * 0.12f;
        if (AmmoLow)
        {
            Centered(handle, Loc.GetString("cmu-target-lock-hud-ammo"), top, size.X);
            top += 40f;
        }

        if (HealthLow)
            Centered(handle, Loc.GetString("cmu-target-lock-hud-health"), top, size.X);
    }

    private void Centered(DrawingHandleScreen handle, string text, float y, float width)
    {
        var dimensions = handle.GetDimensions(_font, text, 1f);
        handle.DrawString(_font, new Vector2((width - dimensions.X) / 2f, y), text, Warning);
    }
}
