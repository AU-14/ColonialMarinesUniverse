using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-505 (I1): the data-core of a Bad Blood bracer. An intel-skilled marine decrypts a bracer no living Bad Blood
/// wears: the Lodge shows on the marine map, the marine learns the ship state, and the bracer becomes retrieve intel.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodBracerDataCoreComponent : Component
{
    [DataField]
    public TimeSpan DecryptTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The RMC skill the decrypting marine needs, at <see cref="SkillLevel"/> or above.
    /// </summary>
    [DataField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillIntel";

    [DataField]
    public int SkillLevel = 1;

    /// <summary>
    /// How long a decryption shows the Lodge on the marine map.
    /// </summary>
    [DataField]
    public TimeSpan RevealTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Intel points the decrypted bracer is worth once carried into a retrieve-item area.
    /// </summary>
    [DataField]
    public FixedPoint2 RetrieveValue = FixedPoint2.New(1);

    [DataField]
    public bool Decrypted;
}

[Serializable, NetSerializable]
public sealed partial class BadBloodBracerDecryptDoAfterEvent : SimpleDoAfterEvent;
