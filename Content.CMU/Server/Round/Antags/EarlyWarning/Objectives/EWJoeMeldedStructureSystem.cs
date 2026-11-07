using Content.Server.SurveillanceCamera;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Radio.Components;
using Content.Shared.SurveillanceCamera.Components;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// ARCH-11: melds a structure the Joe Offered to a Meld directive into the river
/// (<see cref="EWJoeMeldedStructureComponent"/>). Its light is forced on in the Early Warning orange and stays
/// on whatever its power does; a camera is switched off and refuses to come back on (power, EMP recovery); a
/// telecomms server stops being one (<see cref="TelecomServerComponent"/> is removed, so the radio no longer
/// counts it as an active server).
/// </summary>
public sealed class EWJoeMeldedStructureSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SurveillanceCameraSystem _cameras = default!;

    /// <summary>The Early Warning palette's deep orange (<c>EWJoeLightPulseComponent.Deep</c>).</summary>
    public static readonly Color MeldColor = Color.FromHex("#ff7000");

    /// <summary>Wide and bright enough to read as "taken" from across a room, even on a lit map.</summary>
    public const float MeldRadius = 4.5f;

    public const float MeldEnergy = 3.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeMeldedStructureComponent, SurveillanceCameraSetActiveAttemptEvent>(OnCameraActivateAttempt);
        SubscribeLocalEvent<EWJoeMeldedStructureComponent, AttemptPointLightToggleEvent>(OnLightToggleAttempt);
    }

    /// <summary>Melds <paramref name="target"/> into the river on <paramref name="joe"/>'s behalf. Does nothing if it already is.</summary>
    public void Meld(EntityUid target, EntityUid joe)
    {
        if (TerminatingOrDeleted(target) || HasComp<EWJoeMeldedStructureComponent>(target))
            return;

        var melded = AddComp<EWJoeMeldedStructureComponent>(target);
        melded.MeldedBy = joe;

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

    /// <summary>A melded camera never watches again: power returning or an EMP wearing off cannot wake it.</summary>
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
