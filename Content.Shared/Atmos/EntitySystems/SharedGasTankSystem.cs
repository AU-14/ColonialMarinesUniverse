using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Log;
using InternalsComponent = Content.Shared.Body.Components.InternalsComponent;

namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedGasTankSystem : EntitySystem
{
    // CMU14 start
    private const string CMUAnesthesiaSawmillName = "cmu.medical.anesthesia";
    // CMU14 end

    [Dependency] private   SharedActionsSystem _actions = default!;
    [Dependency] private   SharedAudioSystem _audio = default!;
    [Dependency] private   SharedContainerSystem _containers = default!;
    [Dependency] private   SharedInternalsSystem _internals = default!;
    // CMU14 start
    [Dependency] private   ILogManager _log = default!;
    // CMU14 end
    [Dependency] protected SharedUserInterfaceSystem UI = default!;
    [Dependency] private   UseDelaySystem _delay = default!;

    public const string GasTankDelay = "gasTank";

    // CMU14 start
    private ISawmill _anesthesiaSawmill = default!;
    // CMU14 end

    public override void Initialize()
    {
        base.Initialize();

        // CMU14 start
        _anesthesiaSawmill = _log.GetSawmill(CMUAnesthesiaSawmillName);
        // CMU14 end

        SubscribeLocalEvent<GasTankComponent, ComponentShutdown>(OnGasShutdown);
        SubscribeLocalEvent<GasTankComponent, BeforeActivatableUIOpenEvent>(BeforeUiOpen);
        SubscribeLocalEvent<GasTankComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<GasTankComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<GasTankComponent, ToggleActionEvent>(OnActionToggle);
        SubscribeLocalEvent<GasTankComponent, GasTankSetPressureMessage>(OnGasTankSetPressure);
        SubscribeLocalEvent<GasTankComponent, GasTankToggleInternalsMessage>(OnGasTankToggleInternals);
        SubscribeLocalEvent<GasTankComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerb);
    }

    private void OnGasShutdown(Entity<GasTankComponent> gasTank, ref ComponentShutdown args)
    {
        DisconnectFromInternals(gasTank);
    }

    private void OnGasTankToggleInternals(Entity<GasTankComponent> ent, ref GasTankToggleInternalsMessage args)
    {
        // CMU14 start
        DebugGasTank(ent, "ui-toggle-message", args.Actor, null, null);
        // CMU14 end
        ToggleInternals(ent, args.Actor);
    }

    private void OnGasTankSetPressure(Entity<GasTankComponent> ent, ref GasTankSetPressureMessage args)
    {
        var pressure = Math.Clamp(args.Pressure, 0f, ent.Comp.MaxOutputPressure);

        ent.Comp.OutputPressure = pressure;
        Dirty(ent);
        UpdateUserInterface(ent);
    }

    public virtual void UpdateUserInterface(Entity<GasTankComponent> ent)
    {

    }

    private void BeforeUiOpen(Entity<GasTankComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(ent);
    }

    private void OnGetActions(EntityUid uid, GasTankComponent component, GetItemActionsEvent args)
    {
        args.AddAction(ref component.ToggleActionEntity, component.ToggleAction);
        Dirty(uid, component);
    }

    private void OnExamined(EntityUid uid, GasTankComponent component, ExaminedEvent args)
    {
        using var _ = args.PushGroup(nameof(GasTankComponent));

        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("comp-gas-tank-examine", ("pressure", Math.Round(component.Air?.Pressure ?? 0))));

        if (component.IsConnected)
            args.PushMarkup(Loc.GetString("comp-gas-tank-connected"));

        args.PushMarkup(Loc.GetString(component.IsValveOpen ? "comp-gas-tank-examine-open-valve" : "comp-gas-tank-examine-closed-valve"));
    }

    private void OnActionToggle(Entity<GasTankComponent> gasTank, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        // CMU14 start
        DebugGasTank(gasTank, "action-toggle", args.Performer, null, null);
        // CMU14 end
        ToggleInternals(gasTank, user: args.Performer);
        args.Handled = true;
    }

    private void OnGetAlternativeVerb(EntityUid uid, GasTankComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        args.Verbs.Add(new AlternativeVerb()
        {
            Text = component.IsValveOpen ? Loc.GetString("comp-gas-tank-close-valve") : Loc.GetString("comp-gas-tank-open-valve"),
            Act = () =>
            {
                component.IsValveOpen = !component.IsValveOpen;
                _audio.PlayPredicted(component.ValveSound, uid, args.User);
                Dirty(uid, component);
            },
            Disabled = component.IsConnected,
        });
    }

    public bool CanConnectToInternals(Entity<GasTankComponent> ent)
    {
        TryGetInternalsComp(ent, out _, out var internalsComp, ent.Comp.User);
        var canConnect = internalsComp != null && internalsComp.BreathTools.Count != 0 && !ent.Comp.IsValveOpen;

        // CMU14 start
        if (!canConnect)
            DebugGasTank(ent, "can-connect-failed", ent.Comp.User, null, internalsComp);
        // CMU14 end

        return canConnect;
    }

    public bool ConnectToInternals(Entity<GasTankComponent> ent, EntityUid? user = null)
    {
        var (owner, component) = ent;
        // CMU14 start
        DebugGasTank(ent, "connect-start", user, null, null);
        // CMU14 end

        if (component.IsConnected)
        {
            // CMU14 start
            DebugGasTank(ent, "connect-rejected-already-connected", user, component.User, null);
            // CMU14 end
            return false;
        }

        if (!CanConnectToInternals(ent))
        {
            // CMU14 start
            DebugGasTank(ent, "connect-rejected-can-connect", user, null, null);
            // CMU14 end
            return false;
        }

        TryGetInternalsComp(ent, out var internalsUid, out var internalsComp, ent.Comp.User);
        if (internalsUid == null || internalsComp == null)
        {
            // CMU14 start
            DebugGasTank(ent, "connect-rejected-no-internals", user, internalsUid, internalsComp);
            // CMU14 end
            return false;
        }

        if (!_delay.TryResetDelay(ent.Owner, checkDelayed: true, id: GasTankDelay))
        {
            // CMU14 start
            DebugGasTank(ent, "connect-rejected-delay", user, internalsUid, internalsComp);
            // CMU14 end
            return false;
        }

        if (_internals.TryConnectTank((internalsUid.Value, internalsComp), owner))
            component.User = internalsUid.Value;

        Dirty(ent);
        _actions.SetToggled(component.ToggleActionEntity, component.IsConnected);
        _actions.SetCooldown(component.ToggleActionEntity, TimeSpan.FromSeconds(1));

        // Couldn't toggle!
        if (!component.IsConnected)
        {
            // CMU14 start
            DebugGasTank(ent, "connect-failed-after-tryconnect", user, internalsUid, internalsComp);
            // CMU14 end
            return false;
        }

        component.ConnectStream = _audio.Stop(component.ConnectStream);
        component.ConnectStream = _audio.PlayPredicted(component.ConnectSound, owner, user)?.Entity;
        UpdateUserInterface(ent);
        // CMU14 start
        DebugGasTank(ent, "connect-succeeded", user, internalsUid, internalsComp);
        // CMU14 end
        return true;
    }

    /// <summary>
    /// Tries to retrieve the internals component of either the gas tank's user,
    /// or the gas tank's... containing container
    /// </summary>
    /// <param name="user">The user of the gas tank</param>
    /// <returns>True if internals comp isn't null, false if it is null</returns>
    private bool TryGetInternalsComp(Entity<GasTankComponent> ent, out EntityUid? internalsUid, out InternalsComponent? internalsComp, EntityUid? user = null)
    {
        internalsUid = default;
        internalsComp = default;

        // If the gas tank doesn't exist for whatever reason, don't even bother
        if (TerminatingOrDeleted(ent.Owner))
        {
            // CMU14 start
            DebugGasTank(ent, "try-get-internals-deleted", user, null, null);
            // CMU14 end
            return false;
        }

        user ??= ent.Comp.User;
        // Check if the gas tank's user actually has the component that allows them to use a gas tank and mask
        if (TryComp<InternalsComponent>(user, out var userInternalsComp))
        {
            internalsUid = user;
            internalsComp = userInternalsComp;
            // CMU14 start
            DebugGasTank(ent, "try-get-internals-user", user, internalsUid, internalsComp);
            // CMU14 end
            return true;
        }

        // Yeah I have no clue what this actually does, I appreciate the lack of comments on the original function
        if (_containers.TryGetContainingContainer((ent.Owner, Transform(ent.Owner)), out var container))
        {
            if (TryComp<InternalsComponent>(container.Owner, out var containerInternalsComp))
            {
                internalsUid = container.Owner;
                internalsComp = containerInternalsComp;
                // CMU14 start
                DebugGasTank(ent, "try-get-internals-container-owner", user, internalsUid, internalsComp);
                // CMU14 end
                return true;
            }

            // CMU14 start
            DebugGasTank(ent, "try-get-internals-container-no-internals", user, container.Owner, null);
            // CMU14 end
        }

        // CMU14 start
        DebugGasTank(ent, "try-get-internals-none", user, null, null);
        // CMU14 end
        return false;
    }

    public bool DisconnectFromInternals(Entity<GasTankComponent> ent, EntityUid? user = null, bool forced = false)
    {
        var (owner, component) = ent;

        if (component.User == null)
        {
            // CMU14 start
            DebugGasTank(ent, "disconnect-rejected-no-user", user, null, null);
            // CMU14 end
            return false;
        }

        if (!forced && !_delay.TryResetDelay(ent.Owner, checkDelayed: true, id: GasTankDelay))
        {
            // CMU14 start
            DebugGasTank(ent, "disconnect-rejected-delay", user, component.User, null);
            // CMU14 end
            return false;
        }

        TryGetInternalsComp(ent, out var internalsUid, out var internalsComp, component.User);
        component.User = null;
        Dirty(ent);

        _actions.SetToggled(component.ToggleActionEntity, false);

        // I hate this but actions have no easy way to unify this with usedelay.
        if (!forced && _delay.TryGetDelayInfo(ent.Owner, out var delayInfo, id: GasTankDelay))
        {
            _actions.SetCooldown(component.ToggleActionEntity, delayInfo.Length);
        }

        if (internalsUid != null && internalsComp != null)
            _internals.DisconnectTank((internalsUid.Value, internalsComp), forced: forced);

        component.DisconnectStream = _audio.Stop(component.DisconnectStream);
        component.DisconnectStream = _audio.PlayPredicted(component.DisconnectSound, owner, user)?.Entity;
        UpdateUserInterface(ent);
        // CMU14 start
        DebugGasTank(ent, "disconnect-succeeded", user, internalsUid, internalsComp);
        // CMU14 end
        return true;
    }

    private bool ToggleInternals(Entity<GasTankComponent> ent, EntityUid? user = null)
    {
        // CMU14 start
        DebugGasTank(ent, "toggle-start", user, ent.Comp.User, null);
        // CMU14 end

        if (ent.Comp.IsConnected)
        {
            return DisconnectFromInternals(ent, user);
        }
        else
        {
            return ConnectToInternals(ent, user);
        }
    }

    // CMU14 start
    private void DebugGasTank(
        Entity<GasTankComponent> ent,
        string stage,
        EntityUid? actor,
        EntityUid? internalsUid,
        InternalsComponent? internals)
    {
        if (!IsDebugAnestheticTank(ent.Owner))
            return;

        _anesthesiaSawmill.Debug(
            $"[CMU anesthesia] tank-{stage}: tank={DebugEntity(ent.Owner)}, actor={DebugEntity(actor)}, tankUser={DebugEntity(ent.Comp.User)}, requestedInternals={DebugEntity(internalsUid)}, isConnected={ent.Comp.IsConnected}, valveOpen={ent.Comp.IsValveOpen}, output={ent.Comp.OutputPressure:F1}, pressure={ent.Comp.Air.Pressure:F1}, internals={DebugInternals(internals)}");
    }

    private bool IsDebugAnestheticTank(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        var id = MetaData(uid).EntityPrototype?.ID;
        return id?.Contains("Anesthetic", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string DebugInternals(InternalsComponent? internals)
    {
        if (internals == null)
            return "null";

        return $"connectedTank={DebugEntity(internals.GasTankEntity)}, breathTools={internals.BreathTools.Count}";
    }

    private string DebugEntity(EntityUid? uid)
    {
        if (uid == null)
            return "null";

        if (TerminatingOrDeleted(uid.Value))
            return $"{uid.Value} deleted";

        var proto = MetaData(uid.Value).EntityPrototype?.ID ?? "no-proto";

        return $"{ToPrettyString(uid.Value)} proto={proto}";
    }
    // CMU14 end
}
