using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Behaviour for the flesh kudzu tile. Heals abominations standing on it
/// and periodically vents emotes (crying / gasping) for ambience.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationFleshKudzuComponent : Component
{
    /// <summary>How often the heal tick runs.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextHealAt;

    /// <summary>Damage applied (typically negative) to abominations in contact each tick.</summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier Heal = new();

    /// <summary>How often the tendons attempt to infect incapacitated contacts.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan InfectInterval = TimeSpan.FromSeconds(3);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextInfectAt;

    /// <summary>Minimum delay between vocal emotes on this tile (3x rarer than the original tuning).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EmoteIntervalMin = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public TimeSpan EmoteIntervalMax = TimeSpan.FromSeconds(180);

    /// <summary>Probability that an emote tick fires the Crying emote specifically.
    /// Below this roll a Crying emote + cry sound; above, picks from <see cref="Emotes"/>.</summary>
    [DataField, AutoNetworkedField]
    public float CryChance = 0.7f;

    /// <summary>Audio volume offset (dB) applied to the emote sound. Negative is quieter.</summary>
    [DataField, AutoNetworkedField]
    public float EmoteVolume = -8f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextEmoteAt;

    /// <summary>
    /// Non-cry emotes the kudzu can vent — picked from at random when the cry
    /// roll fails. Defaults are existing speech emotes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<EmotePrototype>> Emotes = new()
    {
        "Gasp",
        "Scream",
    };

    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> CryEmote = "Crying";

    /// <summary>
    /// Cry sound collection — played when the Crying emote fires.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier CrySound = new SoundCollectionSpecifier("HumanCry");

    /// <summary>
    /// Sound collections played alongside non-cry emotes (the emote system itself
    /// doesn't play sound for non-humanoid emitters). Pulled at random.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<SoundSpecifier> EmoteSounds = new()
    {
        new SoundCollectionSpecifier("MaleScreams"),
        new SoundCollectionSpecifier("FemaleScreams"),
    };
}
