using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed partial class CMUJeepSystem
{
    private static readonly ResPath ActionsRsi = new("CMU14/Structures/vehicles/jeep/actions.rsi");

    private void InitializeHeadlights()
    {
        SubscribeLocalEvent<CMUVehicleHeadlightsComponent, MapInitEvent>(OnHeadlightsMapInit);
        SubscribeLocalEvent<CMUVehicleHeadlightsComponent, ComponentShutdown>(OnHeadlightsShutdown);
        // RMC flips its spotlight flag on F first; the jeep then moves its switch along to match.
        SubscribeNetworkEvent<VehicleSpotlightToggleRequestEvent>(OnSpotlightToggled, after: [typeof(VehicleSpotlightSystem)]);
    }

    private void OnHeadlightsMapInit(Entity<CMUVehicleHeadlightsComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Beams.Count > 0 && ent.Comp.Beams.TrueForAll(Exists))
            return;

        foreach (var beam in ent.Comp.Beams)
        {
            QueueDel(beam);
        }

        ent.Comp.Beams.Clear();
        foreach (var driver in new[] { true, false })
        {
            ent.Comp.Beams.Add(SpawnBeam(ent, ent.Comp.LowBeamPrototype, CMUHeadlightMode.Low, driver));
            ent.Comp.Beams.Add(SpawnBeam(ent, ent.Comp.HighBeamPrototype, CMUHeadlightMode.High, driver));
        }

        Dirty(ent);
        UpdateBeams(ent);
    }

    private EntityUid SpawnBeam(EntityUid vehicle, string prototype, CMUHeadlightMode mode, bool driver)
    {
        var beam = SpawnAttachedTo(prototype, new EntityCoordinates(vehicle, default));
        var comp = EnsureComp<CMUVehicleHeadlightBeamComponent>(beam);
        comp.Vehicle = vehicle;
        comp.Mode = mode;
        comp.Driver = driver;
        Dirty(beam, comp);

        var follower = EnsureComp<CMUZVisualFollowerComponent>(beam);
        follower.Target = vehicle;
        Dirty(beam, follower);
        return beam;
    }

    private void OnHeadlightsShutdown(Entity<CMUVehicleHeadlightsComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        foreach (var beam in ent.Comp.Beams)
        {
            QueueDel(beam);
        }

        ent.Comp.Beams.Clear();
    }

    /// <summary>
    /// Lights the beam the switch is on, if the headlights are fitted and intact. Server only.
    /// </summary>
    private void UpdateBeams(EntityUid vehicle)
    {
        if (_net.IsClient || !TryComp(vehicle, out CMUVehicleHeadlightsComponent? lights))
            return;

        var works = HeadlightsWork(vehicle);
        foreach (var beam in lights.Beams)
        {
            if (TryComp(beam, out CMUVehicleHeadlightBeamComponent? comp))
                _lights.SetEnabled(beam, works && comp.Mode == lights.Mode);
        }
    }

    /// <summary>
    /// Moves the headlight switch on: off, low beam, high beam, off. Server only, like the hood, so
    /// a mispredicted press never flickers the beam.
    /// </summary>
    public void CycleHeadlights(Entity<CMUVehicleHeadlightsComponent> ent, EntityUid? user)
    {
        if (_net.IsClient)
            return;

        ent.Comp.Mode = ent.Comp.Mode switch
        {
            CMUHeadlightMode.Off => CMUHeadlightMode.Low,
            CMUHeadlightMode.Low => CMUHeadlightMode.High,
            _ => CMUHeadlightMode.Off,
        };
        Dirty(ent);

        // The tail and marker lamps follow RMC's spotlight flag, and so does the F key.
        if (TryComp(ent, out VehicleSpotlightComponent? spotlight))
        {
            spotlight.Enabled = ent.Comp.Mode != CMUHeadlightMode.Off;
            Dirty(ent, spotlight);
        }

        _audio.PlayPvs(ent.Comp.SwitchSound, ent);
        if (user != null)
        {
            var message = ent.Comp.Mode switch
            {
                CMUHeadlightMode.Off => "cmu-jeep-headlights-off",
                CMUHeadlightMode.Low => "cmu-jeep-headlights-low",
                _ => "cmu-jeep-headlights-high",
            };

            if (ent.Comp.Mode != CMUHeadlightMode.Off && !HeadlightsWork(ent.Owner))
                message = "cmu-jeep-headlights-broken";

            _popup.PopupEntity(Loc.GetString(message), ent, user.Value);
        }

        UpdateBeams(ent);
        RefreshHeadlightsAction((ent.Owner, ent.Comp));
    }

    /// <summary>
    /// Shows the switch position on the driver's headlights button.
    /// </summary>
    public void RefreshHeadlightsAction(Entity<CMUVehicleHeadlightsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) ||
            !TryComp(ent, out CMUVehicleDriverActionsComponent? actions) ||
            actions.HeadlightsActionEntity is not { } action)
        {
            return;
        }

        var state = ent.Comp.Mode switch
        {
            CMUHeadlightMode.Off => "headlights_off",
            CMUHeadlightMode.Low => "headlights_low",
            _ => "headlights_high",
        };
        _actions.SetIcon(action, new SpriteSpecifier.Rsi(ActionsRsi, state));
        _actions.SetToggled(action, ent.Comp.Mode != CMUHeadlightMode.Off);
    }

    private void OnSpotlightToggled(VehicleSpotlightToggleRequestEvent ev, EntitySessionEventArgs args)
    {
        if (_net.IsClient)
            return;

        var vehicle = GetEntity(ev.Vehicle);
        if (!TryComp(vehicle, out CMUVehicleHeadlightsComponent? lights) ||
            !TryComp(vehicle, out VehicleSpotlightComponent? spotlight))
        {
            return;
        }

        // RMC only flips the flag for the vehicle's driver; a flag out of step means it did.
        if (spotlight.Enabled != (lights.Mode != CMUHeadlightMode.Off))
            CycleHeadlights((vehicle, lights), args.SenderSession.AttachedEntity);
    }
}
