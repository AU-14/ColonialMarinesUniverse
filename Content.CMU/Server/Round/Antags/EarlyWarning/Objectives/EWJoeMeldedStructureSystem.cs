using Content.Server.Light.Components;
using Content.Server.SurveillanceCamera;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Radio.Components;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// ARCH-11: melds a structure the Joe Offered to a Meld directive into the river
/// (<see cref="EWJoeMeldedStructureComponent"/>). Its light is forced on in the Early Warning orange and stays
/// on whatever its power does; a camera is switched off and refuses to come back on (power, EMP recovery); a
/// telecomms server stops being one (<see cref="TelecomServerComponent"/> is removed, so the radio no longer
/// counts it as an active server).
/// RB-30: a lit welder used on a melded structure starts a <see cref="RepairDelay"/> do-after that burns the
/// meld out: the marker goes, the light it had returns (or the river's is removed), a camera watches again and
/// a server is a server again.
/// </summary>
public sealed class EWJoeMeldedStructureSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SurveillanceCameraSystem _cameras = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    /// <summary>The Early Warning palette's deep orange (<c>EWJoeLightPulseComponent.Deep</c>).</summary>
    public static readonly Color MeldColor = Color.FromHex("#ff7000");

    /// <summary>Wide and bright enough to read as "taken" from across a room, even on a lit map.</summary>
    public const float MeldRadius = 4.5f;

    public const float MeldEnergy = 3.5f;

    /// <summary>RB-30: how long a welder takes to burn a meld out.</summary>
    public static readonly TimeSpan RepairDelay = TimeSpan.FromSeconds(10);

    /// <summary>Welder fuel the repair burns, as the CMU sentry repair does.</summary>
    public const float RepairFuel = 5f;

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeMeldedStructureComponent, SurveillanceCameraSetActiveAttemptEvent>(OnCameraActivateAttempt);
        SubscribeLocalEvent<EWJoeMeldedStructureComponent, AttemptPointLightToggleEvent>(OnLightToggleAttempt);
        SubscribeLocalEvent<EWJoeMeldedStructureComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<EWJoeMeldedStructureComponent, EWJoeMeldRepairDoAfterEvent>(OnRepairDoAfter);
    }

    /// <summary>Melds <paramref name="target"/> into the river on <paramref name="joe"/>'s behalf. Does nothing if it already is.</summary>
    public void Meld(EntityUid target, EntityUid joe)
    {
        if (TerminatingOrDeleted(target) || HasComp<EWJoeMeldedStructureComponent>(target))
            return;

        var melded = AddComp<EWJoeMeldedStructureComponent>(target);
        melded.MeldedBy = joe;
        melded.WasTelecomServer = HasComp<TelecomServerComponent>(target);

        // RB-30: remember the light as it was, so a repair can give it back
        if (_lights.TryGetLight(target, out var existing))
        {
            melded.HadLight = true;
            melded.LightColor = existing.Color;
            melded.LightRadius = existing.Radius;
            melded.LightEnergy = existing.Energy;
            melded.LightEnabled = existing.Enabled;
        }

        var light = _lights.EnsureLight(target);
        _lights.SetColor(target, MeldColor, light);
        _lights.SetRadius(target, MeldRadius, light);
        _lights.SetEnergy(target, MeldEnergy, light);
        _lights.SetEnabled(target, true, light);

        if (TryComp(target, out SurveillanceCameraComponent? camera))
            _cameras.SetActive(target, false, camera);

        // RadioSystem.HasActiveServer queries this component: without it the server relays nothing
        RemComp<TelecomServerComponent>(target);
    }

    /// <summary>
    /// RB-30: burns the meld out of <paramref name="target"/>. The marker goes first, so the camera and light
    /// guards below no longer refuse. Public so tests can skip the do-after.
    /// </summary>
    public void Repair(Entity<EWJoeMeldedStructureComponent?> target)
    {
        if (!Resolve(target, ref target.Comp, false))
            return;

        var melded = target.Comp;
        var uid = target.Owner;
        RemComp<EWJoeMeldedStructureComponent>(uid);

        if (_lights.TryGetLight(uid, out var light))
        {
            if (melded.HadLight)
            {
                _lights.SetColor(uid, melded.LightColor, light);
                _lights.SetRadius(uid, melded.LightRadius, light);
                _lights.SetEnergy(uid, melded.LightEnergy, light);
                // a light that follows its power follows it again; anything else is as it was
                var enabled = HasComp<LitOnPoweredComponent>(uid) ? _power.IsPowered(uid) : melded.LightEnabled;
                _lights.SetEnabled(uid, enabled, light);
            }
            else
            {
                // the river's light was the only one it had
                _lights.SetEnabled(uid, false, light);
                _lights.RemoveLightDeferred(uid);
            }
        }

        if (TryComp(uid, out SurveillanceCameraComponent? camera))
            _cameras.SetActive(uid, true, camera);

        if (melded.WasTelecomServer)
            EnsureComp<TelecomServerComponent>(uid);
    }

    /// <summary>RB-30: a lit welder starts the repair; the tool system checks it is lit and burns its fuel.</summary>
    private void OnInteractUsing(Entity<EWJoeMeldedStructureComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tools.HasQuality(args.Used, WeldingQuality))
            return;

        // the Joe does not undo its own offerings
        if (HasComp<EWJoeComponent>(args.User))
            return;

        args.Handled = true;
        if (_tools.UseTool(args.Used, args.User, ent.Owner, (float) RepairDelay.TotalSeconds, WeldingQuality,
                new EWJoeMeldRepairDoAfterEvent(), fuel: RepairFuel))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-meld-repair-start", ("target", ent.Owner)), ent, args.User);
        }
    }

    private void OnRepairDoAfter(Entity<EWJoeMeldedStructureComponent> ent, ref EWJoeMeldRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Repair((ent.Owner, ent.Comp));
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-meld-repair-done", ("target", ent.Owner)), ent, args.User);
    }

    /// <summary>A melded camera does not watch: power returning or an EMP wearing off cannot wake it.</summary>
    private void OnCameraActivateAttempt(Entity<EWJoeMeldedStructureComponent> ent, ref SurveillanceCameraSetActiveAttemptEvent args)
    {
        args.Cancelled = true;
    }

    /// <summary>The river's glow does not go out with the power (LitOnPowered and the like).</summary>
    private void OnLightToggleAttempt(Entity<EWJoeMeldedStructureComponent> ent, ref AttemptPointLightToggleEvent args)
    {
        if (!args.Enabled)
            args.Cancelled = true;
    }
}
