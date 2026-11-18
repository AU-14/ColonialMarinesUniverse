using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-604: a Bad Blood caste. A Bad Blood chooses one at the Lodge ("Choose your caste") and keeps it for the
/// rest of that life. The caste's powers are the components it adds; the systems that apply them read those
/// components, never the caste id. BadBloodCasteSystem (server).
/// </summary>
[Prototype("badBloodCaste")]
public sealed partial class BadBloodCastePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The caste's name, e.g. "Engineer".
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The caste's line in the Lodge's caste dialog.
    /// </summary>
    [DataField(required: true)]
    public LocId Verb;

    /// <summary>
    /// Told to the Bad Blood who takes the caste up.
    /// </summary>
    [DataField(required: true)]
    public LocId Taken;

    /// <summary>
    /// The role in the name the clan knows its holders by, e.g. "Engineer (BB-12)".
    /// </summary>
    [DataField(required: true)]
    public LocId CodeRole;

    /// <summary>
    /// Skills raised to at least this level when the caste is taken up.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();

    /// <summary>
    /// The caste's powers, added to the Bad Blood when the caste is taken up.
    /// </summary>
    [DataField]
    public ComponentRegistry AddComponents = new();

    /// <summary>
    /// The clan may have one of this caste per this many living Bad Bloods...
    /// </summary>
    [DataField]
    public int CapDivisor = 4;

    /// <summary>
    /// ...and always at least this many.
    /// </summary>
    [DataField]
    public int MinCap = 1;
}
