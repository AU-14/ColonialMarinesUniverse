using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Hypospray;

/// <summary>
/// A movable oxygen-tank dock that holds a tethered hypospray. The injector is an ordinary hypospray, so the
/// medicine lives in its vial.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUHyposprayStationComponent : Component
{
    /// <summary>
    /// The injector spawned into the station on map init.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<CMUHyposprayComponent> InjectorId;

    [DataField]
    public string ContainerId = "cmu_hypospray";

    /// <summary>
    /// How far the injector can be taken from the station before it snaps back.
    /// </summary>
    [DataField]
    public float Range = 5;

    [DataField]
    public SoundSpecifier PickupSound = new SoundPathSpecifier("/Audio/Effects/refill.ogg");
    
    [DataField]
    public SoundSpecifier ReturnSound = new SoundPathSpecifier("/Audio/_RMC14/Handling/gunrustle2.ogg");

    /// <summary>
    /// Played when the tether pulls the injector back on its own.
    /// </summary>
    [DataField]
    public SoundSpecifier SnapSound = new SoundPathSpecifier("/Audio/_RMC14/Handling/gunrustle3.ogg");

    [DataField, AutoNetworkedField]
    public EntityUid? Injector;
}

/// <summary>
/// Ties a hypospray to the station it is wired to.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUHyposprayComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Station;

    /// <summary>
    /// Raw pain each injection adds to the patient. Zero turns the effect off.
    /// </summary>
    [DataField]
    public FixedPoint2 Pain = FixedPoint2.Zero;

    /// <summary>
    /// The highest pain tier the injection may push a patient into. A patient already past it takes no extra pain.
    /// </summary>
    [DataField]
    public PainTier MaxPainTier = PainTier.Mild;
}

/// <summary>
/// Present while the injector is out of its station and subject to the tether.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHyposprayDeployedComponent : Component
{
}
