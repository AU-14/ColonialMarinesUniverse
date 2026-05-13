using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Yautja;

public sealed class YautjaCasterSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly YautjaPowerSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCasterComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<YautjaCasterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaCasterComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<YautjaCasterComponent, GunShotEvent>(OnGunShot);
    }

    private void OnUseInHand(Entity<YautjaCasterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Modes.Count < 2)
            return;

        args.Handled = true;

        if (!HasComp<YautjaComponent>(args.User))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (_net.IsClient)
        {
            PopupMode(ent, args.User, "cmu-yautja-caster-mode-next", (ent.Comp.CurrentMode + 1) % ent.Comp.Modes.Count);
            return;
        }

        ent.Comp.CurrentMode = (ent.Comp.CurrentMode + 1) % ent.Comp.Modes.Count;
        Dirty(ent);
        ApplyMode(ent);
        PopupMode(ent, args.User, "cmu-yautja-caster-mode-set");
    }

    private void OnExamined(Entity<YautjaCasterComponent> ent, ref ExaminedEvent args)
    {
        var mode = GetMode(ent.Comp);
        if (mode == null)
            return;

        args.PushMarkup(Loc.GetString("cmu-yautja-caster-examine-mode",
            ("mode", Loc.GetString(mode.Name)),
            ("power", GetPowerCost(ent.Comp))));
    }

    private void OnAttemptShoot(Entity<YautjaCasterComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<YautjaComponent>(args.User))
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        ApplyMode(ent);
        if (!_power.HasPowerPopup(args.User, GetPowerCost(ent.Comp)))
            args.Cancelled = true;
    }

    private void OnGunShot(Entity<YautjaCasterComponent> ent, ref GunShotEvent args)
    {
        _audio.PlayPredicted(GetFireSound(ent.Comp), ent.Owner, args.User);

        if (!_net.IsClient)
            _power.TryRemovePower(args.User, GetPowerCost(ent.Comp));
    }

    private void ApplyMode(Entity<YautjaCasterComponent> ent)
    {
        var mode = GetMode(ent.Comp);
        if (mode == null)
            return;

        if (!TryComp(ent, out ProjectileBatteryAmmoProviderComponent? ammo) ||
            ammo.Prototype == mode.Projectile)
        {
            return;
        }

        ammo.Prototype = mode.Projectile;
        Dirty(ent, ammo);
    }

    private static YautjaCasterMode? GetMode(YautjaCasterComponent component)
    {
        if (component.Modes.Count == 0)
            return null;

        var mode = component.CurrentMode;
        if (mode < 0 || mode >= component.Modes.Count)
            mode = 0;

        return component.Modes[mode];
    }

    private static FixedPoint2 GetPowerCost(YautjaCasterComponent component)
    {
        return GetMode(component)?.PowerCost ?? component.PowerCost;
    }

    private static Robust.Shared.Audio.SoundSpecifier GetFireSound(YautjaCasterComponent component)
    {
        return GetMode(component)?.FireSound ?? component.FireSound;
    }

    private void PopupMode(Entity<YautjaCasterComponent> ent, EntityUid user, LocId message)
    {
        PopupMode(ent, user, message, ent.Comp.CurrentMode);
    }

    private void PopupMode(Entity<YautjaCasterComponent> ent, EntityUid user, LocId message, int modeIndex)
    {
        var mode = GetMode(ent.Comp);
        if (modeIndex >= 0 && modeIndex < ent.Comp.Modes.Count)
            mode = ent.Comp.Modes[modeIndex];

        if (mode == null)
            return;

        var text = Loc.GetString(message, ("mode", Loc.GetString(mode.Name)));
        if (_net.IsClient)
            _popup.PopupPredicted(text, user, user, PopupType.Medium);
        else
            _popup.PopupClient(text, user, user, PopupType.Medium);
    }
}
