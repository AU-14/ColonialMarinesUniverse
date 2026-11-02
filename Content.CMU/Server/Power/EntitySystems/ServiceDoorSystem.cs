// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Electrocution;
using Content.Shared.CMU14.Power;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Power;

// The service door of a machine: opens and closes with a verb, makes its sound, and blocks the interface and the panel while it is shut.
public sealed class ServiceDoorSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedWiresSystem _wires = default!;

    private static readonly SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/closetopen.ogg");
    private static readonly SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Effects/closetclose.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ServiceDoorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ServiceDoorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ServiceDoorComponent, ActivatableUIOpenAttemptEvent>(OnUiAttempt);
        SubscribeLocalEvent<ServiceDoorComponent, AttemptChangePanelEvent>(OnPanelAttempt);
        SubscribeLocalEvent<ServiceDoorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStartup(Entity<ServiceDoorComponent> door, ref ComponentStartup args)
    {
        Apply(door);
    }

    private void OnGetVerbs(Entity<ServiceDoorComponent> door, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(door.Comp.Open ? "au14-service-door-close" : "au14-service-door-open"),
            Priority = 5,
            Act = () => Toggle(door, user),
        });
    }

    public void Toggle(Entity<ServiceDoorComponent> door, EntityUid user)
    {
        // A door cannot be shut on the opened wiring.
        if (door.Comp.Open && TryComp<WiresPanelComponent>(door, out var panel) && panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("au14-service-door-panel-open"), door, user);
            return;
        }

        door.Comp.Open = !door.Comp.Open;
        Dirty(door);
        Apply(door);

        _audio.PlayPvs(door.Comp.Open ? OpenSound : CloseSound, door);
    }

    // The visual and the live terminals follow the door.
    private void Apply(Entity<ServiceDoorComponent> door)
    {
        _appearance.SetData(door, ServiceDoorVisuals.Open, door.Comp.Open);

        if (TryComp<ElectrifiedComponent>(door, out var electrified))
        {
            electrified.OnHandInteract = door.Comp.Open;
            electrified.OnInteractUsing = door.Comp.Open;
            Dirty(door, electrified);
        }
    }

    private void OnUiAttempt(Entity<ServiceDoorComponent> door, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!door.Comp.Open || args.Cancelled)
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("au14-service-door-ui-blocked"), door, args.User);
    }

    private void OnPanelAttempt(Entity<ServiceDoorComponent> door, ref AttemptChangePanelEvent args)
    {
        // The panel is closed (the event says what it is now) and the door is shut: the wiring cannot be reached.
        if (door.Comp.Open || args.Open || args.Cancelled)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("au14-service-door-wiring-behind"), door, user);
    }

    private void OnExamined(Entity<ServiceDoorComponent> door, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(door.Comp.Open ? "au14-service-door-examine-open" : "au14-service-door-examine-closed"));
    }
}
