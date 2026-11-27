using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Prying.Components;
using Content.Shared.Prying.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.PowerLoader;

/// <summary>
/// Runs <see cref="CMUPowerLoaderComponent"/>. Its piloted visuals are applied on the client.
/// </summary>
public sealed partial class CMUPowerLoaderSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PryingSystem _prying = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPowerLoaderComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<CMUPowerLoaderComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<CMUPowerLoaderComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<CMUPowerLoaderComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CMUPowerLoaderComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CMUPowerLoaderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DoorComponent, PowerLoaderGrabEvent>(OnDoorGrab);
    }

    private void OnMove(Entity<CMUPowerLoaderComponent> ent, ref MoveEvent args)
    {
        // Played by the server only, so prediction can't repeat it.
        if (_net.IsClient ||
            args.ParentChanged ||
            args.OldRotation.GetCardinalDir() == args.NewRotation.GetCardinalDir() ||
            _timing.CurTime < ent.Comp.NextTurnSound)
        {
            return;
        }

        ent.Comp.NextTurnSound = _timing.CurTime + ent.Comp.TurnSoundCooldown;
        _audio.PlayPvs(ent.Comp.TurnSound, ent);
    }

    private void OnStrapped(Entity<CMUPowerLoaderComponent> ent, ref StrappedEvent args)
    {
        _pointLight.SetEnabled(ent, true);
    }

    private void OnUnstrapped(Entity<CMUPowerLoaderComponent> ent, ref UnstrappedEvent args)
    {
        _pointLight.SetEnabled(ent, args.Strap.Comp.BuckledEntities.Count > 0);
    }

    /// <summary>
    /// Clicking a door from the loader forces it with the clamps. The usual door rules apply,
    /// so whether powered doors give way is up to the loader's <see cref="PryingComponent"/>.
    /// </summary>
    private void OnDoorGrab(Entity<DoorComponent> door, ref PowerLoaderGrabEvent args)
    {
        if (args.Handled ||
            !TryComp(args.PowerLoader, out CMUPowerLoaderComponent? loader) ||
            !HasComp<PryingComponent>(args.PowerLoader))
        {
            return;
        }

        args.Handled = true;
        _prying.TryPry(door, args.PowerLoader, out var doAfter, args.PowerLoader);

        var msg = doAfter == null
            ? Loc.GetString("cmu-power-loader-pry-fail", ("door", door))
            : Loc.GetString("cmu-power-loader-pry-start", ("door", door));
        foreach (var pilot in args.Buckled)
        {
            _popup.PopupClient(msg, door, pilot, doAfter == null ? PopupType.SmallCaution : PopupType.Small);
        }

        // The door's sound slot is cut off by RMC if the pry is cancelled.
        if (doAfter != null && _net.IsServer && loader.PrySound != null)
        {
            door.Comp.SoundEntity = _audio.Stop(door.Comp.SoundEntity);
            door.Comp.SoundEntity = _audio.PlayPvs(loader.PrySound, door)?.Entity;
        }
    }

    private void OnDamageChanged(Entity<CMUPowerLoaderComponent> ent, ref DamageChangedEvent args)
    {
        // The state is networked, so only the server works it out.
        if (_net.IsClient)
            return;

        var damage = _damageable.GetTotalDamage(ent.Owner);
        var state = -1;
        for (var i = 0; i < ent.Comp.DamageStates.Count; i++)
        {
            if (damage >= ent.Comp.DamageStates[i].Damage)
                state = i;
        }

        if (state == ent.Comp.DamageState)
            return;

        var worse = state > ent.Comp.DamageState;
        ent.Comp.DamageState = state;
        Dirty(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        if (!worse)
            return;

        // One spark as it crosses into the new state, not a constant shower.
        var reached = ent.Comp.DamageStates[state];
        if (reached.Effect is { } effect)
            SpawnAtPosition(effect, Transform(ent).Coordinates);

        _audio.PlayPvs(reached.Sound, ent);

        if (reached.Popup is not { } popup || !TryComp(ent, out StrapComponent? strap))
            return;

        var popupMsg = Loc.GetString(popup, ("mech", ent.Owner));
        foreach (var pilot in strap.BuckledEntities)
        {
            _popup.PopupEntity(popupMsg, pilot, pilot, PopupType.MediumCaution);
        }
    }

    private void OnRefreshSpeed(Entity<CMUPowerLoaderComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (GetDamageState(ent) is { } state)
            args.ModifySpeed(state.SpeedModifier, state.SpeedModifier);
    }

    private void OnExamined(Entity<CMUPowerLoaderComponent> ent, ref ExaminedEvent args)
    {
        if (GetDamageState(ent)?.Examine is { } examine)
            args.PushMarkup(Loc.GetString(examine, ("mech", ent.Owner)));
    }

    private static CMUPowerLoaderDamageState? GetDamageState(CMUPowerLoaderComponent comp)
    {
        return comp.DamageState >= 0 && comp.DamageState < comp.DamageStates.Count
            ? comp.DamageStates[comp.DamageState]
            : null;
    }
}
