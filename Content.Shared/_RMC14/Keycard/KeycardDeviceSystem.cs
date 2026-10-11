using Content.Shared._RMC14.AlertLevel;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Keycard;

public sealed partial class KeycardDeviceSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private RMCAlertLevelSystem _alertLevel = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<KeycardDeviceComponent>> _devices = new();

    // CMU14: severity ladder for stepping. Green/blue/red move one rung per ceremony;
    // levels outside it (delta as current, yellow and future members as targets)
    // bypass the ladder and jump directly. Order is fixed here, not by enum value,
    // so new members can be inserted anywhere in the enum.
    private static readonly RMCAlertLevels[] AlertLadder =
        [RMCAlertLevels.Green, RMCAlertLevels.Blue, RMCAlertLevels.Red];

    public override void Initialize()
    {
        SubscribeLocalEvent<KeycardDeviceComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<KeycardDeviceComponent, KeycardDeviceSetModeEvent>(OnSetMode);
        SubscribeLocalEvent<KeycardDeviceComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractHand(Entity<KeycardDeviceComponent> ent, ref InteractHandEvent args)
    {
        if (!_accessReader.IsAllowed(args.User, ent))
        {
            _popup.PopupClient(Loc.GetString("rmc-access-denied"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        // TODO RMC14 ERT, enable/disable maintenance security
        // CMU14: offer every alert level except delta and the current one.
        var current = _alertLevel.Get(ent) ?? RMCAlertLevels.Green; // CMU14
        var targets = new List<RMCAlertLevels>();
        for (var i = AlertLadder.Length - 1; i >= 0; i--)
            targets.Add(AlertLadder[i]);
        foreach (var level in Enum.GetValues<RMCAlertLevels>())
        {
            if (!AlertLadder.Contains(level))
                targets.Add(level);
        }

        var options = new List<DialogOption>();
        foreach (var level in targets)
        {
            if (level == RMCAlertLevels.Delta
                || level == current)
                continue;

            var text = Loc.GetString($"rmc-alert-{level.ToString().ToLowerInvariant()}");
            options.Add(new DialogOption(text, new KeycardDeviceSetModeEvent(level)));
        }

        // CMU14: state the current level in the dialog message
        var message = Loc.GetString("rmc-keycard-device-current",
            ("level", Loc.GetString($"rmc-alert-{current.ToString().ToLowerInvariant()}")));
        _dialog.OpenOptions(ent,
            args.User,
            Loc.GetString("rmc-keycard-device"),
            options,
            message
        );
    }

    private void OnSetMode(Entity<KeycardDeviceComponent> ent, ref KeycardDeviceSetModeEvent args)
    {
        ent.Comp.Mode = args.Mode;
        Dirty(ent);
    }

    private void OnInteractUsing(Entity<KeycardDeviceComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp(ent, out AccessReaderComponent? accessReader))
        {
            var access = _accessReader.FindAccessTags(args.Used);
            if (!_accessReader.AreAccessTagsAllowed(access, accessReader))
            {
                _popup.PopupClient(Loc.GetString("rmc-access-denied"), ent, args.User, PopupType.SmallCaution);
                return;
            }
        }

        var time = _timing.CurTime;

        // CMU14: cluster cooldown between alert steps, one step per ceremony
        if (ent.Comp.LastStep is { } lastStep && lastStep + ent.Comp.Cooldown > time)
        {
            var remaining = lastStep + ent.Comp.Cooldown - time;
            _popup.PopupClient(Loc.GetString("rmc-keycard-device-cooldown",
                ("seconds", (int) remaining.TotalSeconds)), ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.LastActivated = time;
        Dirty(ent);

        if (!AllEnabled(ent))
            return;

        // CMU14: was a fixed red alert switch, now steps one level toward the armed target
        // switch (ent.Comp.Mode)
        // {
        //     case KeycardDeviceMode.None:
        //         return;
        //     case KeycardDeviceMode.RedAlert:
        //         _alertLevel.Set(RMCAlertLevels.Red, args.User);
        //         break;
        //     default:
        //         Log.Warning($"Unknown {nameof(KeycardDeviceMode)}: {ent.Comp.Mode}");
        //         return;
        // }
        if (ent.Comp.Mode is not { } target)
            return;

        var current = _alertLevel.Get(ent) ?? RMCAlertLevels.Green; // CMU14
        if (target == current)
        {
            var name = Loc.GetString($"rmc-alert-{current.ToString().ToLowerInvariant()}");
            _popup.PopupClient(Loc.GetString("rmc-keycard-device-already", ("level", name)),
                ent, args.User, PopupType.SmallCaution);
            return;
        }

        // CMU14: ladder levels step one rung toward the target
        var currentIdx = Array.IndexOf(AlertLadder, current);
        var targetIdx = Array.IndexOf(AlertLadder, target);
        var step = currentIdx < 0 || targetIdx < 0
            ? target
            : AlertLadder[currentIdx + Math.Sign(targetIdx - currentIdx)];

        // CMU14: stamp the cooldown on every device in the cluster, not just this one
        _devices.Clear();
        _entityLookup.GetEntitiesInRange(ent.Owner.ToCoordinates(), ent.Comp.Range, _devices);
        foreach (var device in _devices)
        {
            device.Comp.LastStep = time;
            Dirty(device);
        }

        _alertLevel.Set(step, args.User);
    }

    private bool AllEnabled(Entity<KeycardDeviceComponent> ent)
    {
        _devices.Clear();
        _entityLookup.GetEntitiesInRange(ent.Owner.ToCoordinates(), ent.Comp.Range, _devices);

        var time = _timing.CurTime;
        foreach (var device in _devices)
        {
            if (ent.Comp.Mode != device.Comp.Mode)
                return false;

            if (device.Comp.LastActivated < time - device.Comp.Time)
                return false;
        }

        return true;
    }
}
