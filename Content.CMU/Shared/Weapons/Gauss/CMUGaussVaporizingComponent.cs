using Content.Shared.Decals;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Weapons.Gauss;

/// <summary>
///     On someone an overcharged M60 gauss gun is vaporizing. They're frozen in place, turn black and shed
///     embers and ash, then are gone at <see cref="End"/>, leaving only a pair of smoking boots.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUGaussVaporizingComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan End;

    /// <summary>How long they stand blackened before they're gone.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(2);

    /// <summary>How far, in tiles, the gun is flung back from where they were facing.</summary>
    [DataField]
    public float FlingDistance = 1.5f;

    [DataField]
    public float FlingSpeed = 4f;

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/acid_sizzle1.ogg");

    /// <summary>
    ///     The only cry they make; their own screams and pain sounds are silenced.
    ///     Female characters use <see cref="ScreamFemale"/> instead.
    /// </summary>
    [DataField]
    public SoundSpecifier Scream = new SoundPathSpecifier("/Audio/Voice/Human/wilhelm_scream.ogg");

    [DataField]
    public SoundSpecifier ScreamFemale = new SoundPathSpecifier("/Audio/Voice/Human/femalescream_3.ogg");

    [DataField]
    public SoundSpecifier EndSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");

    /// <summary>Left behind if they weren't wearing any boots.</summary>
    [DataField]
    public EntProtoId Boots = "CMBootsBlack";

    [DataField]
    public EntProtoId Smoke = "CMUGaussVaporizeSmoke";

    [DataField]
    public ProtoId<DecalPrototype> Decal = "RMCScorch4";
    
    /// <summary>Inventory slots whose items survive, flung away from where they stood, instead of being deleted.</summary>
    [DataField]
    public List<string> ThrownSlots = new() { "back", "id" };

    // How far, in tiles, and how fast the surviving items are flung.
    [DataField]
    public float ThrowDistanceMin = 4f;

    [DataField]
    public float ThrowDistanceMax = 6f;

    [DataField]
    public float ThrowSpeed = 12f;
}
