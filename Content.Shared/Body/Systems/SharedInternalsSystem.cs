using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Internals;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Shared.Body.Systems;

/// <summary>
/// Handles lung breathing with gas tanks for entities.
/// </summary>
public abstract partial class SharedInternalsSystem : EntitySystem
{
    // CMU14 start
    private const string CMUAnesthesiaSawmillName = "cmu.medical.anesthesia";
    // CMU14 end

    [Dependency] private AlertsSystem _alerts = default!;
    // CMU14 start
    [Dependency] private ILogManager _log = default!;
    // CMU14 end
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGasTankSystem _gasTank = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;

    // CMU14 start
    private ISawmill _anesthesiaSawmill = default!;
    // CMU14 end

    public override void Initialize()
    {
        base.Initialize();

        // CMU14 start
        _anesthesiaSawmill = _log.GetSawmill(CMUAnesthesiaSawmillName);
        // CMU14 end

        SubscribeLocalEvent<InternalsComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);

        SubscribeLocalEvent<InternalsComponent, ComponentStartup>(OnInternalsStartup);
        SubscribeLocalEvent<InternalsComponent, ComponentShutdown>(OnInternalsShutdown);

        SubscribeLocalEvent<InternalsComponent, InternalsDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<InternalsComponent, ToggleInternalsAlertEvent>(OnToggleInternalsAlert);
    }

    private void OnGetInteractionVerbs(
        Entity<InternalsComponent> ent,
        ref GetVerbsEvent<InteractionVerb> args)
    {
        // RMC14
        if (HasComp<XenoComponent>(args.User))
            return;

        if (!args.CanAccess || !args.CanInteract || args.Hands is null)
            return;

        if (!AreInternalsWorking(ent) && ent.Comp.BreathTools.Count == 0)
        {
            // CMU14 start
            DebugInternals("verb-hidden-no-breath-tool", ent.Owner, args.User, ent.Comp, ToggleMode.Toggle, false, null);
            // CMU14 end
            return;
        }

        var user = args.User;

        InteractionVerb verb = new()
        {
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
        };

        if (AreInternalsWorking(ent))
        {
            // CMU14 start
            DebugInternals("verb-added-off", ent.Owner, user, ent.Comp, ToggleMode.Off, false, ent.Comp.GasTankEntity);
            // CMU14 end
            verb.Act = () => ToggleInternals(ent, user, force: false, ent, ToggleMode.Off);
            verb.Message = Loc.GetString("action-description-internals-toggle-off");
            verb.Text = Loc.GetString("action-name-internals-toggle-off");
        }
        else
        {
            // CMU14 start
            DebugInternals("verb-added-on", ent.Owner, user, ent.Comp, ToggleMode.On, false, null);
            // CMU14 end
            verb.Act = () => ToggleInternals(ent, user, force: false, ent, ToggleMode.On);
            verb.Message = Loc.GetString("action-description-internals-toggle-on");
            verb.Text = Loc.GetString("action-name-internals-toggle-on");
        }

        args.Verbs.Add(verb);
    }

    protected bool ToggleInternals(
        EntityUid target,
        EntityUid user,
        bool force,
        InternalsComponent? internals = null,
        ToggleMode mode = ToggleMode.Toggle)
    {
        if (!Resolve(target, ref internals, logMissing: false))
        {
            // CMU14 start
            DebugInternals("toggle-resolve-failed", target, user, null, mode, force, null);
            // CMU14 end
            return false;
        }

        // CMU14 start
        DebugInternals("toggle-start", target, user, internals, mode, force, null);
        // CMU14 end

        // Check if a mask is present.
        if (internals.BreathTools.Count == 0)
        {
            // CMU14 start
            DebugInternals("toggle-no-breath-tool", target, user, internals, mode, force, null);
            // CMU14 end

            var message = user == target ? Loc.GetString("internals-self-no-breath-tool") : Loc.GetString("internals-other-no-breath-tool", ("ent", Identity.Name(target, EntityManager, user)));
            _popupSystem.PopupClient(message, target, user);
            return false;
        }

        // Check if tank is present.
        var tank = FindBestGasTank(target);

        // If they're not on then check if we have a mask to use
        if (tank == null)
        {
            // CMU14 start
            DebugInternals("toggle-no-tank", target, user, internals, mode, force, null);
            // CMU14 end

            var message = user == target ? Loc.GetString("internals-self-no-tank") : Loc.GetString("internals-other-no-tank", ("ent", Identity.Name(target, EntityManager, user)));
            _popupSystem.PopupClient(message, target, user);
            return false;
        }

        // CMU14 start
        DebugInternals("toggle-found-tank", target, user, internals, mode, force, tank.Value.Owner);
        // CMU14 end

        // Start the toggle do-after if it's on someone else.
        if (!force && user != target)
        {
            // CMU14 start
            DebugInternals("toggle-start-doafter", target, user, internals, mode, force, tank.Value.Owner);
            // CMU14 end
            return StartToggleInternalsDoAfter(user, (target, internals), mode);
        }

        // Toggle off.
        if (TryComp(internals.GasTankEntity, out GasTankComponent? gas))
        {
            if (mode == ToggleMode.On)
            {
                // CMU14 start
                DebugInternals("toggle-rejected-already-connected", target, user, internals, mode, force, internals.GasTankEntity);
                // CMU14 end
                return false;
            }

            // CMU14 start
            DebugInternals("toggle-disconnect", target, user, internals, mode, force, internals.GasTankEntity);
            // CMU14 end
            return _gasTank.DisconnectFromInternals((internals.GasTankEntity.Value, gas), user);
        }

        // No tank was connected, we’ll try to toggle internals on

        // If the intent was to disable internals there’s nothing left to do
        if (mode == ToggleMode.Off)
        {
            // CMU14 start
            DebugInternals("toggle-rejected-off-without-tank", target, user, internals, mode, force, null);
            // CMU14 end
            return false;
        }

        var connected = _gasTank.ConnectToInternals(tank.Value, user: user);
        // CMU14 start
        DebugInternals(connected ? "toggle-connect-succeeded" : "toggle-connect-failed", target, user, internals, mode, force, tank.Value.Owner);
        // CMU14 end
        return connected;
    }

    private bool StartToggleInternalsDoAfter(EntityUid user, Entity<InternalsComponent> targetEnt, ToggleMode mode)
    {
        // Is the target not you? If yes, use a do-after to give them time to respond.
        var isUser = user == targetEnt.Owner;
        var delay = !isUser ? targetEnt.Comp.Delay : TimeSpan.Zero;

        return _doAfter.TryStartDoAfter(
            new DoAfterArgs(EntityManager, user, delay, new InternalsDoAfterEvent(mode), targetEnt, target: targetEnt)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                MovementThreshold = 0.1f,
            });
    }

    private void OnDoAfter(Entity<InternalsComponent> ent, ref InternalsDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        ToggleInternals(ent, args.User, force: true, ent, args.ToggleMode);

        args.Handled = true;
    }

    private void OnToggleInternalsAlert(Entity<InternalsComponent> ent, ref ToggleInternalsAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled |= ToggleInternals(ent, ent, false, internals: ent.Comp);
    }

    private void OnInternalsStartup(Entity<InternalsComponent> ent, ref ComponentStartup args)
    {
        ShowInternalsAlert(ent, GetSeverity(ent));

        // CMU14 start
        var ev = new InternalsGasTankChangedEvent(ent.Owner, ent.Comp.GasTankEntity);
        RaiseLocalEvent(ent.Owner, ref ev);
        // CMU14 end
    }

    private void OnInternalsShutdown(Entity<InternalsComponent> ent, ref ComponentShutdown args)
    {
        ClearInternalsAlert(ent);

        // CMU14 start
        var ev = new InternalsGasTankChangedEvent(ent.Owner, null);
        RaiseLocalEvent(ent.Owner, ref ev);
        // CMU14 end
    }

    public void ConnectBreathTool(Entity<InternalsComponent> ent, EntityUid toolEntity)
    {
        if (!ent.Comp.BreathTools.Add(toolEntity))
            return;

        if (TryComp(toolEntity, out BreathToolComponent? breathTool))
        {
            breathTool.ConnectedInternalsEntity = ent.Owner;
            Dirty(toolEntity, breathTool);
        }

        Dirty(ent);
        ShowInternalsAlert(ent, GetSeverity(ent));

        // CMU14 start
        DebugInternals("breath-tool-connected", ent.Owner, ent.Owner, ent.Comp, ToggleMode.Toggle, true, null);
        // CMU14 end
    }

    public void DisconnectBreathTool(Entity<InternalsComponent> ent, EntityUid toolEntity, bool forced = false)
    {
        if (!ent.Comp.BreathTools.Remove(toolEntity))
            return;

        Dirty(ent);

        if (TryComp(toolEntity, out BreathToolComponent? breathTool))
        {
            breathTool.ConnectedInternalsEntity = null;
            Dirty(toolEntity, breathTool);
        }

        if (ent.Comp.BreathTools.Count == 0)
        {
            DisconnectTank(ent, forced: forced);
        }

        ShowInternalsAlert(ent, GetSeverity(ent));

        // CMU14 start
        DebugInternals(forced ? "breath-tool-disconnected-forced" : "breath-tool-disconnected", ent.Owner, ent.Owner, ent.Comp, ToggleMode.Toggle, true, null);
        // CMU14 end
    }

    public void DisconnectTank(Entity<InternalsComponent> ent, bool forced = false)
    {
        if (TryComp(ent.Comp.GasTankEntity, out GasTankComponent? tank))
            _gasTank.DisconnectFromInternals((ent.Comp.GasTankEntity.Value, tank), forced: forced);

        ent.Comp.GasTankEntity = null;
        Dirty(ent);
        ShowInternalsAlert(ent, GetSeverity(ent.Comp));

        // CMU14 start
        var ev = new InternalsGasTankChangedEvent(ent.Owner, null);
        RaiseLocalEvent(ent.Owner, ref ev);
        // CMU14 end
    }

    public bool TryConnectTank(Entity<InternalsComponent> ent, EntityUid tankEntity)
    {
        if (ent.Comp.BreathTools.Count == 0)
            return false;

        if (TryComp(ent.Comp.GasTankEntity, out GasTankComponent? tank))
            _gasTank.DisconnectFromInternals((ent.Comp.GasTankEntity.Value, tank));

        ent.Comp.GasTankEntity = tankEntity;
        Dirty(ent);
        ShowInternalsAlert(ent, GetSeverity(ent));

        // CMU14 start
        var ev = new InternalsGasTankChangedEvent(ent.Owner, tankEntity);
        RaiseLocalEvent(ent.Owner, ref ev);
        // CMU14 end
        return true;
    }

    public bool AreInternalsWorking(EntityUid uid, InternalsComponent? component = null)
    {
        return Resolve(uid, ref component, logMissing: false)
               && AreInternalsWorking(component);
    }

    public bool AreInternalsWorking(InternalsComponent component)
    {
        return TryComp(component.BreathTools.FirstOrNull(), out BreathToolComponent? breathTool)
               && breathTool.IsFunctional
               && HasComp<GasTankComponent>(component.GasTankEntity);
    }

    protected short GetSeverity(InternalsComponent component)
    {
        if (component.BreathTools.Count == 0 || !AreInternalsWorking(component))
            return 2;

        // If pressure in the tank is below low pressure threshold, flash warning on internals UI
        if (TryComp<GasTankComponent>(component.GasTankEntity, out var gasTank)
            && gasTank.IsLowPressure)
        {
            return 0;
        }

        return 1;
    }

    // CMU14 start
    protected void ShowInternalsAlert(Entity<InternalsComponent> ent, short severity)
    {
        if (HasComp<HumanMedicalComponent>(ent.Owner))
        {
            _alerts.ClearAlert(ent, ent.Comp.InternalsAlert);
            return;
        }

        _alerts.ShowAlert(ent, ent.Comp.InternalsAlert, severity);
    }

    protected void ClearInternalsAlert(Entity<InternalsComponent> ent)
    {
        _alerts.ClearAlert(ent, ent.Comp.InternalsAlert);
    }
    // CMU14 end

    public Entity<GasTankComponent>? FindBestGasTank(
        Entity<HandsComponent?, InventoryComponent?, ContainerManagerComponent?> user)
    {
        // TODO use _respirator.CanMetabolizeGas() to prioritize metabolizable gasses
        // Prioritise
        // 1. back equipped tanks
        // 2. exo-slot tanks
        // 3. in-hand tanks
        // 4. pocket/belt tanks

        if (!Resolve(user, ref user.Comp2, ref user.Comp3))
            return null;

        if (_inventory.TryGetSlotEntity(user, "back", out var backEntity, user.Comp2, user.Comp3) &&
            TryComp<GasTankComponent>(backEntity, out var backGasTank) &&
            _gasTank.CanConnectToInternals((backEntity.Value, backGasTank)))
        {
            return (backEntity.Value, backGasTank);
        }

        if (_inventory.TryGetSlotEntity(user, "suitstorage", out var entity, user.Comp2, user.Comp3) &&
            TryComp<GasTankComponent>(entity, out var gasTank) &&
            _gasTank.CanConnectToInternals((entity.Value, gasTank)))
        {
            return (entity.Value, gasTank);
        }

        foreach (var item in _inventory.GetHandOrInventoryEntities((user.Owner, user.Comp1, user.Comp2)))
        {
            if (TryComp(item, out gasTank) && _gasTank.CanConnectToInternals((item, gasTank)))
                return (item, gasTank);
        }

        return null;
    }

    // CMU14 start
    private void DebugInternals(
        string stage,
        EntityUid target,
        EntityUid user,
        InternalsComponent? internals,
        ToggleMode mode,
        bool force,
        EntityUid? tank)
    {
        _anesthesiaSawmill.Debug(
            $"[CMU anesthesia] internals-{stage}: target={DebugEntity(target)}, user={DebugEntity(user)}, mode={mode}, force={force}, connectedTank={DebugEntity(internals?.GasTankEntity)}, candidateTank={DebugEntity(tank)}, breathTools={internals?.BreathTools.Count ?? -1}, tools=[{DebugBreathTools(internals)}], maskSlot={DebugMaskSlot(target)}");
    }

    private string DebugBreathTools(InternalsComponent? internals)
    {
        if (internals == null)
            return "internals=null";

        if (internals.BreathTools.Count == 0)
            return "none";

        var tools = new List<string>(internals.BreathTools.Count);
        foreach (var tool in internals.BreathTools)
        {
            var hasBreathTool = TryComp<BreathToolComponent>(tool, out var breathTool);
            var hasMask = TryComp<MaskComponent>(tool, out _);
            tools.Add(
                $"{DebugEntity(tool)} breathTool={hasBreathTool} connected={DebugEntity(breathTool?.ConnectedInternalsEntity)} allowed={breathTool?.AllowedSlots.ToString() ?? "n/a"} maskComp={hasMask}");
        }

        return string.Join("; ", tools);
    }

    private string DebugMaskSlot(EntityUid target)
    {
        if (!_inventory.TryGetSlotEntity(target, "mask", out var mask))
            return "empty-or-no-mask-slot";

        var hasBreathTool = TryComp<BreathToolComponent>(mask.Value, out var breathTool);
        var hasMask = TryComp<MaskComponent>(mask.Value, out _);
        return
            $"{DebugEntity(mask)} breathTool={hasBreathTool} connected={DebugEntity(breathTool?.ConnectedInternalsEntity)} allowed={breathTool?.AllowedSlots.ToString() ?? "n/a"} maskComp={hasMask}";
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

// CMU14 start
[ByRefEvent]
public readonly record struct InternalsGasTankChangedEvent(EntityUid Body, EntityUid? Tank);
// CMU14 end
